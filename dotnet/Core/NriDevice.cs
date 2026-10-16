// L2 — the device.
//
// Everything in NRI hangs off a Device: the function tables are acquired per device, every object
// is created on one, and the message and allocation callbacks are installed at creation. The
// wrapper holds the Core and Helper tables by value (both are always available) and acquires the
// extension tables on first use, each exactly once.
//
// Threading, per NRI.h: Create* is free-threaded; Destroy* and Cmd* are not; GetQueue is a Create*
// in disguise (it returns a pre-created queue and, on Vulkan, flips resource sharing for compute
// and copy queues, so call it before creating resources that cross queues).

using System.Runtime.InteropServices;
using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>An NRI device on one backend. Create with <see cref="Create"/>; dispose last.</summary>
public sealed unsafe partial class NriDevice : NriObject
{
    private readonly CoreInterface _core;
    private readonly HelperInterface _helper;

    private readonly DeviceDesc _desc;
    private readonly MessageBridge _messages;
    private readonly AllocatorBridge? _allocator;
    private readonly INriAllocator? _allocatorInstance;
    private readonly NriQueue?[][] _queues;
    private int _children;
    private string? _lastError;

    private NriDevice(Device* handle, MessageBridge messages, AllocatorBridge? allocator, INriAllocator? allocatorInstance)
        : base(handle, "NriDevice")
    {
        _messages = messages;
        _allocator = allocator;
        _allocatorInstance = allocatorInstance;

        _core = InterfaceTable.Acquire<CoreInterface>(handle, "CoreInterface");
        _helper = InterfaceTable.Acquire<HelperInterface>(handle, "HelperInterface");
        _desc = *_core.GetDeviceDesc(handle);

        _queues = new NriQueue?[(int)QueueType.MAX_NUM][];
        for (var i = 0; i < _queues.Length; i++) _queues[i] = new NriQueue?[_desc.adapterDesc.queueNum[i]];
    }

    // ---------------------------------------------------------------- creation

    /// <summary>Creates a device. The options are copied; nothing in them needs to outlive the call.</summary>
    public static NriDevice Create(NriDeviceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var recorder = new ErrorRecorder(options.MessageHandler);
        var messages = new MessageBridge(recorder.Handle, options.BreakOnError);
        var allocator = options.Allocator is null ? null : new AllocatorBridge(options.Allocator, options.ForwardAllocatorToThirdParty);

        var families = options.QueueFamilies;
        var familyDescs = new QueueFamilyDesc[families.Count];
        var pins = new List<GCHandle>();
        using var instanceExtensions = new Utf8.StringArray(options.VkInstanceExtensions);
        using var deviceExtensions = new Utf8.StringArray(options.VkDeviceExtensions);
        using var agilityPath = new Utf8.Scoped(ResolveAgilitySdkPath(options));

        try
        {
            for (var i = 0; i < families.Count; i++)
            {
                var f = families[i];
                float* priorities = null;
                if (f.Priorities is { Length: > 0 } p)
                {
                    if (p.Length != f.Count) throw new ArgumentException($"QueueFamilies[{i}]: {p.Length} priorities for {f.Count} queues", nameof(options));
                    var pin = GCHandle.Alloc(p, GCHandleType.Pinned);
                    pins.Add(pin);
                    priorities = (float*)pin.AddrOfPinnedObject();
                }
                familyDescs[i] = new QueueFamilyDesc { queueType = f.Type, queueNum = f.Count, queuePriorities = priorities };
            }

            AdapterDesc adapterDesc = options.Adapter?.Desc ?? default;

            fixed (QueueFamilyDesc* familyPtr = familyDescs)
            {
                var desc = new DeviceCreationDesc
                {
                    graphicsAPI = options.GraphicsApi,
                    robustness = options.Robustness,
                    deviceLostInfoLevel = options.DeviceLostInfoLevel,
                    adapterDesc = options.Adapter is null ? null : &adapterDesc,
                    callbackInterface = messages.Native,
                    allocationCallbacks = allocator?.Native ?? default,
                    queueFamilies = families.Count == 0 ? null : familyPtr,
                    queueFamilyNum = (uint)families.Count,
                    d3dShaderExtRegister = options.D3DShaderExtRegister,
                    d3dZeroBufferSize = options.D3DZeroBufferSize,
                    d3d12AgilitySdkPath = agilityPath,
                    vkBindingOffsets = options.VkBindingOffsets,
                    vkExtensions = new VKExtensions
                    {
                        instanceExtensions = instanceExtensions.Pointer,
                        instanceExtensionNum = instanceExtensions.Count,
                        deviceExtensions = deviceExtensions.Pointer,
                        deviceExtensionNum = deviceExtensions.Count,
                    },
                    enableNRIValidation = Bool(options.EnableNriValidation),
                    enableGraphicsAPIValidation = Bool(options.EnableGraphicsApiValidation),
                    enableD3D12RayTracingValidation = Bool(options.EnableD3D12RayTracingValidation),
                    enableD3D12GpuBasedValidation = Bool(options.EnableD3D12GpuBasedValidation),
                    enableMemoryZeroInitialization = Bool(options.EnableMemoryZeroInitialization),
                    disableVKRayTracing = Bool(options.DisableVkRayTracing),
                    disableD3D12EnhancedBarriers = Bool(options.DisableD3D12EnhancedBarriers),
                    // The debug layer's break follows MessageBridge's: a debugger only (patches/0018).
                    disableD3D12BreakOnError = Bool(!(options.BreakOnError && System.Diagnostics.Debugger.IsAttached)),
                };

                Device* handle = null;
                var result = NriApi.nriCreateDevice(&desc, &handle);
                if (result != Result.SUCCESS || handle is null)
                {
                    throw new NriException(result, "nriCreateDevice", recorder.LastError);
                }

                var device = new NriDevice(handle, messages, allocator, options.Allocator);
                recorder.Attach(device);
                messages = null!;
                allocator = null;
                if (options.GraphicsApi == GraphicsAPI.VK && options.VkEnumeratePhysicalDevicesAfterCreation) EnumeratePhysicalDevicesVK(device);
                return device;
            }
        }
        catch
        {
            messages?.Dispose();
            allocator?.Dispose();
            throw;
        }
        finally
        {
            foreach (var pin in pins) pin.Free();
        }
    }

    // RivaTuner's implicit Vulkan layer maps each VkPhysicalDevice to its instance from
    // vkEnumeratePhysicalDevices and uses that map when its overlay initialises at the first
    // vkQueuePresentKHR. NRI finds physical devices through vkEnumeratePhysicalDeviceGroups only
    // (Creation.cpp, DeviceVK::Create), so the layer has no entry for NRI's device and the process
    // dies at the first present. One count-then-fill call on NRI's instance, through the loader NRI
    // loaded, results discarded. Never throws; no managed allocation for up to 32 physical devices.
    // Not on the wrapped route: that instance is the caller's. See CLAUDE.md: Device creation and
    // callbacks.
    private static void EnumeratePhysicalDevicesVK(NriDevice device)
    {
        NriWrapperVKExtension? wrapper;
        try { wrapper = device.TryWrapperVK; }
        catch (NriException) { return; }
        if (wrapper is null) return;

        var instance = wrapper.GetInstanceVK();
        var getInstanceProcAddr = (delegate* unmanaged[Stdcall]<nint, byte*, nint>)wrapper.GetInstanceProcAddrVK();
        if (instance == 0 || getInstanceProcAddr is null) return;

        delegate* unmanaged[Stdcall]<nint, uint*, nint*, int> enumerate;
        fixed (byte* name = "vkEnumeratePhysicalDevices"u8)
        {
            enumerate = (delegate* unmanaged[Stdcall]<nint, uint*, nint*, int>)getInstanceProcAddr(instance, name);
        }
        if (enumerate is null) return;

        uint count = 0;
        if (enumerate(instance, &count, null) < 0 || count == 0) return; // VkResult errors are negative

        const int StackCapacity = 32;
        if (count <= StackCapacity)
        {
            var physicalDevices = stackalloc nint[StackCapacity];
            var filled = count;
            _ = enumerate(instance, &filled, physicalDevices);
        }
        else
        {
            var physicalDevices = new nint[count];
            fixed (nint* p = physicalDevices)
            {
                var filled = count;
                _ = enumerate(instance, &filled, p);
            }
        }
    }

    /// <summary>The directory the native package stages the Agility SDK into, when present.</summary>
    public static string? DefaultAgilitySdkPath
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return null;
            var beside = Path.Combine(AppContext.BaseDirectory, "AgilitySDK");
            return File.Exists(Path.Combine(beside, "D3D12Core.dll")) ? beside + Path.DirectorySeparatorChar : null;
        }
    }

    private static string? ResolveAgilitySdkPath(NriDeviceOptions options)
    {
        if (options.GraphicsApi != GraphicsAPI.D3D12) return null;
        return options.D3D12AgilitySdkPath switch
        {
            null => DefaultAgilitySdkPath,
            "" => null,
            var explicitPath => explicitPath.EndsWith(Path.DirectorySeparatorChar) || explicitPath.EndsWith('/') ? explicitPath : explicitPath + Path.DirectorySeparatorChar,
        };
    }

    // ---------------------------------------------------------------- properties

    public override NriDevice Device => this;

    /// <summary>The native device pointer, for the wrapper extensions and native-object escape hatches.</summary>
    internal Device* Raw => (Device*)Handle;

    /// <summary>
    /// NRI's <c>CoreInterface</c> for this device, as acquired at creation. Its entries take NRI
    /// handles (<see cref="NriObject.NriHandle"/>); the binding does not see calls made through it.
    /// Last resort.
    /// </summary>
    public ref readonly CoreInterface Core => ref _core;

    /// <summary>NRI's <c>HelperInterface</c> for this device. As <see cref="Core"/>: last resort.</summary>
    public ref readonly HelperInterface Helper => ref _helper;

    /// <summary>Capabilities, limits and the adapter, as NRI filled them at creation.</summary>
    public ref readonly DeviceDesc Desc => ref _desc;

    public GraphicsAPI GraphicsApi => _desc.graphicsAPI;

    /// <summary>The <c>NRI_VERSION</c> the native library was built from.</summary>
    public ushort NriVersion => _desc.nriVersion;

    /// <summary>The allocator the device was created with, or null for NRI's own.</summary>
    public INriAllocator? Allocator => _allocatorInstance;

    /// <summary>The last error message NRI reported on this device, or null.</summary>
    public string? LastError => Volatile.Read(ref _lastError);

    /// <summary>Objects created on this device and not yet disposed.</summary>
    public int LiveObjects => Volatile.Read(ref _children);

    // ---------------------------------------------------------------- queues

    /// <summary>
    /// One of the queues the device was created with. Owned by the device, never disposed.
    /// Throws <see cref="NriUnsupportedException"/> when there is no queue of that type.
    /// </summary>
    public NriQueue GetQueue(QueueType type, uint index = 0)
    {
        var slots = _queues[(int)type];
        if (index < slots.Length && slots[index] is { } existing) return existing;

        Queue* queue = null;
        Check(Core.GetQueue(Raw, type, index, &queue), "GetQueue");
        var wrapped = new NriQueue(this, queue, type, index);
        if (index < slots.Length) slots[index] = wrapped;
        return wrapped;
    }

    /// <summary>Blocks until every queue is idle.</summary>
    public void WaitIdle() => Check(Core.DeviceWaitIdle(Raw), "DeviceWaitIdle");

    public FormatSupportBits GetFormatSupport(Format format) => Core.GetFormatSupport(Raw, format);

    /// <summary>
    /// Backend diagnostics for a lost device. Only meaningful after <see cref="NriDeviceLostException"/>
    /// and with a <see cref="DeviceLostInfoLevel"/> above <c>NONE</c>. The bytes are NRI-owned
    /// and valid until the device is destroyed.
    /// </summary>
    public ReadOnlySpan<byte> ReportDeviceLostInfo()
    {
        DeviceLostDump dump = default;
        Check(NriApi.nriReportDeviceLostInfo(Raw, &dump), "nriReportDeviceLostInfo");
        return dump.data is null ? default : new ReadOnlySpan<byte>(dump.data, checked((int)dump.size));
    }

    /// <summary>
    /// The native device: <c>ID3D12Device*</c> on D3D12, <c>VkDevice</c> on Vulkan. Last resort.
    /// </summary>
    public nint GetNativeObject() => (nint)Core.GetDeviceNativeObject(Raw);

    // ---------------------------------------------------------------- bookkeeping

    internal void ChildCreated() => Interlocked.Increment(ref _children);
    internal void ChildDestroyed() => Interlocked.Decrement(ref _children);

    internal void Check(Result result, string call)
    {
        if (result != Result.SUCCESS) throw Fail(result, call);
    }

    internal NriException Fail(Result result, string call)
    {
        var detail = LastError;
        return result switch
        {
            Result.DEVICE_LOST => new NriDeviceLostException(call),
            Result.UNSUPPORTED => new NriUnsupportedException(call),
            _ => new NriException(result, call, detail),
        };
    }

    /// <summary>
    /// Refuses while any object created on this device is still alive: destroying the device
    /// first would turn every later child disposal into a use-after-free.
    /// </summary>
    private protected override void BeforeDestroy()
    {
        var live = Volatile.Read(ref _children);
        if (live > 0)
        {
            throw new InvalidOperationException(
                $"NriDevice disposed with {live} object(s) still alive. Dispose everything created on the device first.");
        }
    }

    private protected override void Destroy(void* handle) => NriApi.nriDestroyDevice((Device*)handle);

    private protected override void OnDestroyed()
    {
        // After nriDestroyDevice: NRI may still report leaked objects through the callback while
        // destroying, so the bridges outlive the native device by exactly this much.
        _messages.Dispose();
        _allocator?.Dispose();
    }

    private sealed class ErrorRecorder(NriMessageHandler? forward)
    {
        private NriDevice? _device;
        private string? _lastError;

        public string? LastError => _lastError;

        public void Attach(NriDevice device)
        {
            _device = device;
            if (_lastError is not null) Volatile.Write(ref device._lastError, _lastError);
        }

        public NriMessageHandler Handle => Record;

        private void Record(in NriMessage message)
        {
            if (message.IsError)
            {
                _lastError = message.Text;
                if (_device is { } d) Volatile.Write(ref d._lastError, message.Text);
            }
            forward?.Invoke(in message);
        }
    }
}
