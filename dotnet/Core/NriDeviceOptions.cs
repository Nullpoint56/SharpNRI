// L2 — what a device is created with.
//
// A managed mirror of DeviceCreationDesc, field for field, so that nothing that needs pinning
// (queue families, extension name arrays, the adapter desc, the callback structs) has to be
// pinned by the caller. The names are NRI's; the defaults are NRI's.

using PrismNRI.Native;

namespace PrismNRI;

/// <summary>One family of queues to create: a type and how many.</summary>
/// <param name="Type">Graphics, compute, copy, video decode or encode.</param>
/// <param name="Count">How many. Must not exceed <see cref="NriAdapter.QueueCount"/>.</param>
/// <param name="Priorities">
/// Optional, one per queue, in [-1, 1]: low below 0, normal 0, high above 0.
/// </param>
public readonly record struct NriQueueFamily(QueueType Type, uint Count = 1, float[]? Priorities = null);

/// <summary>Options for <see cref="NriDevice.Create"/>. Property names follow <c>DeviceCreationDesc</c>.</summary>
public sealed class NriDeviceOptions
{
    /// <summary>Exactly one backend bit: <c>VK</c>, <c>D3D12</c> or <c>NONE</c>.</summary>
    public required GraphicsAPI GraphicsApi { get; init; }

    /// <summary>
    /// The adapter to create on; NRI picks when null: the first adapter of its own enumeration that
    /// supports <see cref="GraphicsApi"/>. For Vulkan that enumeration runs the Vulkan pass only
    /// (patch 0024), so creating a Vulkan device loads no D3D12 runtime; for D3D12 it is the full
    /// enumeration (<see cref="NriAdapter.Enumerate()"/>), as before, so the desc stays the one NRI gave.
    /// </summary>
    public NriAdapter? Adapter { get; init; }

    public Robustness Robustness { get; init; }

    /// <summary>D3D12: process-global; the first device created decides for the process.</summary>
    public DeviceLostInfoLevel DeviceLostInfoLevel { get; init; }

    /// <summary>
    /// Queue families to create. One graphics queue when empty, which is NRI's default. Put the
    /// graphics family first. Requesting compute or copy queues switches Vulkan resources created
    /// without <c>queueExclusive</c> to concurrent sharing; see the guide.
    /// </summary>
    public IReadOnlyList<NriQueueFamily> QueueFamilies { get; init; } = [];

    /// <summary>Receives every message NRI produces. The device records the last error regardless.</summary>
    public NriMessageHandler? MessageHandler { get; init; }

    /// <summary>
    /// Break into an attached debugger after an error message. No-op without one. Also governs the
    /// D3D12 debug layer's own break (<c>ID3D12InfoQueue::SetBreakOnSeverity</c> for ERROR and
    /// CORRUPTION, which NRI sets whenever <see cref="EnableGraphicsApiValidation"/> is on): NRI
    /// sets it only when this is true and a debugger is attached when the device is created
    /// (<c>disableD3D12BreakOnError</c>, patches/0018). Otherwise a D3D12 debug-layer error arrives
    /// as an error message (<c>[id] text</c>) instead of a <c>DebugBreak</c> that surfaces as an SEH
    /// exception out of the NRI call, or kills a process with no debugger.
    /// </summary>
    public bool BreakOnError { get; init; }

    /// <summary>Host allocator for NRI's bookkeeping. NRI's own when null.</summary>
    public INriAllocator? Allocator { get; init; }

    /// <summary>
    /// Also route VMA and D3D12MA host allocations through <see cref="Allocator"/>. Off by
    /// default: those allocators are chatty and the usual reason to supply one is accounting
    /// NRI's own use. Maps to the inverse of <c>disable3rdPartyAllocationCallbacks</c>.
    /// </summary>
    public bool ForwardAllocatorToThirdParty { get; init; }

    /// <summary>D3D: register for vendor shader extensions, space 0. NRI's default when 0.</summary>
    public uint D3DShaderExtRegister { get; init; }

    /// <summary>D3D: scratch size for <c>CmdZeroBuffer</c> emulation. 4 MB when 0.</summary>
    public uint D3DZeroBufferSize { get; init; }

    /// <summary>
    /// D3D12: the directory holding the Agility SDK's <c>D3D12Core.dll</c>. Null means the
    /// <c>AgilitySDK</c> directory beside the PrismNRI assembly when it exists (the native package
    /// stages it there), otherwise the inbox runtime. An empty string forces the inbox runtime.
    /// A managed host cannot export <c>D3D12SDKVersion</c>, so this is the only way the SDK loads;
    /// see CLAUDE.md: D3D12 and the Agility SDK, and patches/0001.
    /// </summary>
    public string? D3D12AgilitySdkPath { get; init; }

    /// <summary>
    /// Vulkan: after NRI creates the device, call <c>vkEnumeratePhysicalDevices</c> once on NRI's
    /// instance and discard the results. NRI discovers physical devices only through device groups,
    /// and at least one implicit layer (RivaTuner's) maps physical devices to instances from
    /// <c>vkEnumeratePhysicalDevices</c> and kills the process at the first present without it; the
    /// call is one loader round trip. Not applied by <see cref="NriDevice.CreateFromVulkan"/>, whose
    /// instance is the caller's.
    /// </summary>
    public bool VkEnumeratePhysicalDevicesAfterCreation { get; init; } = true;

    /// <summary>
    /// Vulkan: the per-register-class binding shifts applied to every descriptor range. All zero
    /// means the shader's binding is the register index. Must match what the shader compiler was
    /// given.
    /// </summary>
    public VKBindingOffsets VkBindingOffsets { get; init; }

    public IReadOnlyList<string> VkInstanceExtensions { get; init; } = [];
    public IReadOnlyList<string> VkDeviceExtensions { get; init; } = [];

    /// <summary>NRI's embedded validation. Checks NRI's own rules, both backends.</summary>
    public bool EnableNriValidation { get; init; }

    /// <summary>The backend's validation: Vulkan validation layers or the D3D12 debug layer. D3D12: process-global.</summary>
    public bool EnableGraphicsApiValidation { get; init; }

    /// <summary>D3D12 only, slow, and needs <c>NV_ALLOW_RAYTRACING_VALIDATION=1</c> in the environment.</summary>
    public bool EnableD3D12RayTracingValidation { get; init; }

    /// <summary>
    /// D3D12: GPU-based validation (<c>ID3D12Debug1::SetEnableGPUBasedValidation</c>, patches/0019),
    /// which checks descriptors and resource states while shaders execute. Slow. Needs
    /// <see cref="EnableGraphicsApiValidation"/>; ignored without it. Process-global like the debug
    /// layer and never switched off again: every D3D12 device created later in the process runs
    /// with it, whatever its own options say.
    /// </summary>
    public bool EnableD3D12GpuBasedValidation { get; init; }

    /// <summary>Clear memory on allocation. Off by default on both backends.</summary>
    public bool EnableMemoryZeroInitialization { get; init; }

    /// <summary>Vulkan: skip ray-tracing extensions to save host memory.</summary>
    public bool DisableVkRayTracing { get; init; }

    /// <summary>D3D12: use legacy barriers even with the Agility SDK present.</summary>
    public bool DisableD3D12EnhancedBarriers { get; init; }
}
