// L2 — devices wrapped around a native device the caller created (NRIWrapperVK.h,
// NRIWrapperD3D12.h): nriCreateDeviceFromVKDevice and nriCreateDeviceFromD3D12Device.
//
// The same bridges as NriDevice.Create: the message callback (with the device recording the last
// error), the optional host allocator, and the lifetime rule that both outlive the native device.
// What differs is what NRI is told: the native device, its instance and physical device or its
// queues, and which extensions the caller enabled, because NRI did not create any of them. The
// caller's native device must outlive the NriDevice.
//
// No Agility SDK path on the D3D12 form: the caller's ID3D12Device already exists, so the D3D12
// runtime it was created on is the one in use (DeviceCreationD3D12Desc has no such field either).
// Neither form has a graphics-API validation switch: the caller decides that when creating the
// native device.

using System.Runtime.InteropServices;
using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>Options for <see cref="NriDevice.CreateFromVulkan"/>. Property names follow <c>DeviceCreationVKDesc</c>.</summary>
public sealed class NriVulkanDeviceOptions
{
    /// <summary>The caller's <c>VkInstance</c>.</summary>
    public required nint VkInstance { get; init; }

    /// <summary>The caller's <c>VkDevice</c>. Must outlive the NRI device.</summary>
    public required nint VkDevice { get; init; }

    /// <summary>The <c>VkPhysicalDevice</c> <see cref="VkDevice"/> was created on. Required.</summary>
    public required nint VkPhysicalDevice { get; init; }

    /// <summary>
    /// The queues the caller created, by NRI queue type and Vulkan family index. NRI fetches them
    /// with <c>vkGetDeviceQueue2</c>; only these exist on the NRI device.
    /// </summary>
    public IReadOnlyList<QueueFamilyVKDesc> QueueFamilies { get; init; } = [];

    /// <summary>The Vulkan minor version the device was created for; must be at least 2.</summary>
    public byte MinorVersion { get; init; } = 2;

    /// <summary>
    /// The instance and device extensions the caller enabled. NRI derives what it may use from
    /// these lists and from the physical device's features, so every listed extension must be
    /// enabled, and every core feature NRI finds supported should be enabled too.
    /// </summary>
    public IReadOnlyList<string> VkInstanceExtensions { get; init; } = [];

    /// <inheritdoc cref="VkInstanceExtensions"/>
    public IReadOnlyList<string> VkDeviceExtensions { get; init; } = [];

    /// <summary>Optional Vulkan loader path; NRI's default loader name when null.</summary>
    public string? LibraryPath { get; init; }

    /// <summary>Per-register-class binding shifts; see <see cref="NriDeviceOptions.VkBindingOffsets"/>.</summary>
    public VKBindingOffsets VkBindingOffsets { get; init; }

    /// <summary>Receives every message NRI produces. The device records the last error regardless.</summary>
    public NriMessageHandler? MessageHandler { get; init; }

    /// <summary>Break into an attached debugger after an error message.</summary>
    public bool BreakOnError { get; init; }

    /// <summary>Host allocator for NRI's bookkeeping. NRI's own when null.</summary>
    public INriAllocator? Allocator { get; init; }

    /// <summary>Also route third-party (VMA) host allocations through <see cref="Allocator"/>.</summary>
    public bool ForwardAllocatorToThirdParty { get; init; }

    /// <summary>NRI's embedded validation.</summary>
    public bool EnableNriValidation { get; init; }

    /// <summary>Clear memory on allocation, where the device supports zero-initialized memory.</summary>
    public bool EnableMemoryZeroInitialization { get; init; }
}

/// <summary>Options for <see cref="NriDevice.CreateFromD3D12"/>. Property names follow <c>DeviceCreationD3D12Desc</c>.</summary>
public sealed class NriD3D12DeviceOptions
{
    /// <summary>The caller's <c>ID3D12Device*</c>. Must outlive the NRI device.</summary>
    public required nint D3D12Device { get; init; }

    /// <summary>
    /// The queue families, with the caller's <c>ID3D12CommandQueue*</c>s or none (NRI creates
    /// them). Unlike <see cref="NriDevice.Create"/>, an empty list means no queues at all: NRI
    /// does not default to a graphics queue here (measured at v190).
    /// </summary>
    public IReadOnlyList<NriQueueFamilyD3D12Desc> QueueFamilies { get; init; } = [];

    /// <summary>Optional <c>AGSContext*</c> the caller initialized (AMD).</summary>
    public nint AgsContext { get; init; }

    /// <summary>Receives every message NRI produces. The device records the last error regardless.</summary>
    public NriMessageHandler? MessageHandler { get; init; }

    /// <summary>Break into an attached debugger after an error message.</summary>
    public bool BreakOnError { get; init; }

    /// <summary>Host allocator for NRI's bookkeeping. NRI's own when null.</summary>
    public INriAllocator? Allocator { get; init; }

    /// <summary>Also route third-party (D3D12MA) host allocations through <see cref="Allocator"/>.</summary>
    public bool ForwardAllocatorToThirdParty { get; init; }

    /// <summary>Register for vendor shader extensions, space 0. NRI's default when 0.</summary>
    public uint D3DShaderExtRegister { get; init; }

    /// <summary>Scratch size for <c>CmdZeroBuffer</c> emulation. 4 MB when 0.</summary>
    public uint D3DZeroBufferSize { get; init; }

    /// <summary>NRI's embedded validation.</summary>
    public bool EnableNriValidation { get; init; }

    /// <summary>Clear memory on allocation. Off by default to match Vulkan.</summary>
    public bool EnableMemoryZeroInitialization { get; init; }

    /// <summary>Use legacy barriers even with the Agility SDK present; some integrations still do.</summary>
    public bool DisableD3D12EnhancedBarriers { get; init; }

    /// <summary>
    /// Skip <c>NvAPI_Initialize</c>. NVAPI must be initialized in the module where the device is
    /// created, so a caller that already did may want NRI not to.
    /// </summary>
    public bool DisableNvapiInitialization { get; init; }
}

public sealed unsafe partial class NriDevice
{
    /// <summary>
    /// <c>nriCreateDeviceFromVKDevice</c>: an NRI device over the caller's <c>VkDevice</c>. The
    /// options are copied; the Vulkan objects they name must outlive the device.
    /// </summary>
    public static NriDevice CreateFromVulkan(NriVulkanDeviceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var recorder = new ErrorRecorder(options.MessageHandler);
        var messages = new MessageBridge(recorder.Handle, options.BreakOnError);
        var allocator = options.Allocator is null ? null : new AllocatorBridge(options.Allocator, options.ForwardAllocatorToThirdParty);

        var families = options.QueueFamilies.ToArray();
        using var instanceExtensions = new Utf8.StringArray(options.VkInstanceExtensions);
        using var deviceExtensions = new Utf8.StringArray(options.VkDeviceExtensions);
        using var libraryPath = new Utf8.Scoped(options.LibraryPath);

        try
        {
            fixed (QueueFamilyVKDesc* familyPtr = families)
            {
                var desc = new DeviceCreationVKDesc
                {
                    callbackInterface = messages.Native,
                    allocationCallbacks = allocator?.Native ?? default,
                    libraryPath = libraryPath,
                    vkBindingOffsets = options.VkBindingOffsets,
                    vkExtensions = new VKExtensions
                    {
                        instanceExtensions = instanceExtensions.Pointer,
                        instanceExtensionNum = instanceExtensions.Count,
                        deviceExtensions = deviceExtensions.Pointer,
                        deviceExtensionNum = deviceExtensions.Count,
                    },
                    vkInstance = (void*)options.VkInstance,
                    vkDevice = (void*)options.VkDevice,
                    vkPhysicalDevice = (void*)options.VkPhysicalDevice,
                    queueFamilies = families.Length == 0 ? null : familyPtr,
                    queueFamilyNum = (uint)families.Length,
                    minorVersion = options.MinorVersion,
                    enableNRIValidation = Bool(options.EnableNriValidation),
                    enableMemoryZeroInitialization = Bool(options.EnableMemoryZeroInitialization),
                };

                Device* handle = null;
                var result = NriApi.nriCreateDeviceFromVKDevice(&desc, &handle);
                if (result != Result.SUCCESS || handle is null)
                {
                    throw new NriException(result, "nriCreateDeviceFromVKDevice", recorder.LastError);
                }

                var device = new NriDevice(handle, messages, allocator, options.Allocator);
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
    }

    /// <summary>
    /// <c>nriCreateDeviceFromD3D12Device</c>: an NRI device over the caller's <c>ID3D12Device</c>.
    /// No Agility SDK path is applied: the device already exists on whichever runtime the process
    /// loaded. The options are copied; the COM objects they name must outlive the device.
    /// </summary>
    public static NriDevice CreateFromD3D12(NriD3D12DeviceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var recorder = new ErrorRecorder(options.MessageHandler);
        var messages = new MessageBridge(recorder.Handle, options.BreakOnError);
        var allocator = options.Allocator is null ? null : new AllocatorBridge(options.Allocator, options.ForwardAllocatorToThirdParty);

        var families = options.QueueFamilies;
        var familyDescs = new QueueFamilyD3D12Desc[families.Count];
        var pins = new List<GCHandle>();

        try
        {
            for (var i = 0; i < families.Count; i++)
            {
                var f = families[i];
                ID3D12CommandQueue** queues = null;
                if (f.D3D12Queues is { Length: > 0 } q)
                {
                    if (q.Length != f.QueueNum) throw new ArgumentException($"QueueFamilies[{i}]: {q.Length} queues for queueNum {f.QueueNum}", nameof(options));
                    var pin = GCHandle.Alloc(q, GCHandleType.Pinned);
                    pins.Add(pin);
                    queues = (ID3D12CommandQueue**)pin.AddrOfPinnedObject();
                }
                familyDescs[i] = new QueueFamilyD3D12Desc { d3d12Queues = queues, queueNum = f.QueueNum, queueType = f.QueueType };
            }

            fixed (QueueFamilyD3D12Desc* familyPtr = familyDescs)
            {
                var desc = new DeviceCreationD3D12Desc
                {
                    d3d12Device = (ID3D12Device*)options.D3D12Device,
                    queueFamilies = familyDescs.Length == 0 ? null : familyPtr,
                    queueFamilyNum = (uint)familyDescs.Length,
                    agsContext = (AGSContext*)options.AgsContext,
                    callbackInterface = messages.Native,
                    allocationCallbacks = allocator?.Native ?? default,
                    d3dShaderExtRegister = options.D3DShaderExtRegister,
                    d3dZeroBufferSize = options.D3DZeroBufferSize,
                    enableNRIValidation = Bool(options.EnableNriValidation),
                    enableMemoryZeroInitialization = Bool(options.EnableMemoryZeroInitialization),
                    disableD3D12EnhancedBarriers = Bool(options.DisableD3D12EnhancedBarriers),
                    disableNVAPIInitialization = Bool(options.DisableNvapiInitialization),
                };

                Device* handle = null;
                var result = NriApi.nriCreateDeviceFromD3D12Device(&desc, &handle);
                if (result != Result.SUCCESS || handle is null)
                {
                    throw new NriException(result, "nriCreateDeviceFromD3D12Device", recorder.LastError);
                }

                var device = new NriDevice(handle, messages, allocator, options.Allocator);
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
        }
    }
}
