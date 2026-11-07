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
using SharpNRI.Interop;
using SharpNRI.Native;

namespace SharpNRI;

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
        var agilitySdkPath = ResolveAgilitySdkPath(options); // may refuse: before anything needs disposing

        var recorder = new ErrorRecorder(options.MessageHandler);
        var messages = new MessageBridge(recorder.Handle, options.BreakOnError);
        var allocator = options.Allocator is null ? null : new AllocatorBridge(options.Allocator, options.ForwardAllocatorToThirdParty);

        var families = options.QueueFamilies;
        var familyDescs = new QueueFamilyDesc[families.Count];
        var pins = new List<GCHandle>();
        using var instanceExtensions = new Utf8.StringArray(options.VkInstanceExtensions);
        using var deviceExtensions = new Utf8.StringArray(options.VkDeviceExtensions);
        using var agilityPath = new Utf8.Scoped(agilitySdkPath);

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
                    vkInstanceCreateInfoNext = (void*)options.VkInstanceCreateInfoNext,
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

                var device = Adopt(handle, messages, allocator, options.Allocator);
                recorder.Attach(device);
                messages = null!;
                allocator = null;
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
            NriException.ClearReported(); // creation's own messages are no later call's reason
        }
    }

    // Takes over a device NRI just created: acquires its tables and holds its NRI version to the
    // binding's (Interop/NriVersionCheck.cs). On any failure the native device is destroyed here,
    // before the caller's catch disposes the bridges it calls into.
    private static NriDevice Adopt(Device* handle, MessageBridge messages, AllocatorBridge? allocator, INriAllocator? allocatorInstance)
    {
        NriDevice device;
        try
        {
            device = new NriDevice(handle, messages, allocator, allocatorInstance);
        }
        catch (NriException e) when (e.Result == Result.INVALID_ARGUMENT)
        {
            // nriGetInterface refuses a table of another size: a library of another NRI version
            NriApi.nriDestroyDevice(handle);
            throw NriVersionCheck.TableMismatch(e);
        }
        catch
        {
            NriApi.nriDestroyDevice(handle);
            throw;
        }

        if (NriVersionCheck.Mismatch(device._desc.nriVersion) is { } mismatch)
        {
            NriApi.nriDestroyDevice(handle);
            throw mismatch;
        }
        return device;
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

    /// <summary>
    /// The runtime switch the SharpNRI.AgilitySDK package sets: the application ships the Agility SDK,
    /// so a missing <c>AgilitySDK</c> directory is an error, not a fall back to the inbox runtime.
    /// </summary>
    internal const string RequireAgilitySdkSwitch = "SharpNRI.RequireAgilitySdk";

    private static string? ResolveAgilitySdkPath(NriDeviceOptions options) =>
        ResolveAgilitySdkPath(options, DefaultAgilitySdkPath, AppContext.TryGetSwitch(RequireAgilitySdkSwitch, out var required) && required);

    internal static string? ResolveAgilitySdkPath(NriDeviceOptions options, string? defaultPath, bool required)
    {
        if (options.GraphicsApi != GraphicsAPI.D3D12) return null;
        return options.D3D12AgilitySdkPath switch
        {
            null when defaultPath is null && required => throw new NriException(Result.INVALID_SDK, "nriCreateDevice",
                $"the application references SharpNRI.AgilitySDK ({RequireAgilitySdkSwitch}), but {Path.Combine(AppContext.BaseDirectory, "AgilitySDK", "D3D12Core.dll")} is missing: " +
                "the build copies it (check the build output for SHARPNRI01x), or pass D3D12AgilitySdkPath = \"\" for the inbox runtime"),
            null => defaultPath,
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

    /// <summary>
    /// The last error message NRI or a validation layer reported on this device, on any thread, or
    /// null. A record of the device, not of one call: an <see cref="NriException"/> carries only the
    /// text reported on its own thread since the binding last checked a result there.
    /// </summary>
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
        NriException.Check(Core.GetQueue(Raw, type, index, &queue), "GetQueue");
        var wrapped = new NriQueue(this, queue, type, index);
        if (index < slots.Length) slots[index] = wrapped;
        return wrapped;
    }

    /// <summary>Blocks until every queue is idle.</summary>
    public void WaitIdle() => NriException.Check(Core.DeviceWaitIdle(Raw), "DeviceWaitIdle");

    public FormatSupportBits GetFormatSupport(Format format) => Core.GetFormatSupport(Raw, format);

    /// <summary>
    /// Backend diagnostics for a lost device. Only meaningful after <see cref="NriDeviceLostException"/>
    /// and with a <see cref="DeviceLostInfoLevel"/> above <c>NONE</c>. The bytes are NRI-owned
    /// and valid until the device is destroyed.
    /// </summary>
    public ReadOnlySpan<byte> ReportDeviceLostInfo()
    {
        DeviceLostDump dump = default;
        NriException.Check(NriApi.nriReportDeviceLostInfo(Raw, &dump), "nriReportDeviceLostInfo");
        return dump.data is null ? default : new ReadOnlySpan<byte>(dump.data, checked((int)dump.size));
    }

    /// <summary>
    /// The native device: <c>ID3D12Device*</c> on D3D12, <c>VkDevice</c> on Vulkan. Last resort.
    /// </summary>
    public nint GetNativeObject() => (nint)Core.GetDeviceNativeObject(Raw);

    // ---------------------------------------------------------------- bookkeeping

    internal void ChildCreated() => Interlocked.Increment(ref _children);
    internal void ChildDestroyed() => Interlocked.Decrement(ref _children);

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
