// L2 — adapters, before there is a device.
//
// nriEnumerateAdapters fills up to the caller's count and returns how many it copied; since NRI
// 962615b (#286) it takes the backends to probe. The desc is kept whole because nriCreateDevice
// takes a pointer to it to select the adapter; the properties are the same bytes read out by name.

using SharpNRI.Interop;
using SharpNRI.Native;

namespace SharpNRI;

/// <summary>A physical adapter NRI can create a device on.</summary>
public sealed class NriAdapter
{
    private readonly AdapterDesc _desc;

    private NriAdapter(in AdapterDesc desc)
    {
        _desc = desc;
        Name = Utf8.ReadFixed(desc.name);
    }

    /// <summary>The desc as NRI reported it. Pass to <see cref="NriDeviceOptions.Adapter"/> by this object.</summary>
    public ref readonly AdapterDesc Desc => ref _desc;

    public string Name { get; }

    /// <summary>LUID when <c>high</c> is 0, UUID otherwise. What a compositor publishes to match on.</summary>
    public Uid_t Uid => _desc.uid;

    public Vendor Vendor => _desc.vendor;
    public Architecture Architecture => _desc.architecture;

    /// <summary>A mask: the backends that can create a device on this adapter.</summary>
    public GraphicsAPI SupportedGraphicsApis => _desc.supportedGraphicsAPIs;

    public ulong VideoMemorySize => _desc.videoMemorySize;
    public ulong SharedSystemMemorySize => _desc.sharedSystemMemorySize;
    public uint DeviceId => _desc.deviceId;
    public uint DriverVersion => _desc.driverVersion;

    /// <summary>How many queues of a type the adapter exposes. D3D advertises a fixed 4 per type.</summary>
    public uint QueueCount(QueueType type) => _desc.queueNum[(int)type];

    /// <summary>True for WARP, lavapipe and other CPU implementations.</summary>
    public bool IsSoftware => _desc.architecture == Architecture.SOFTWARE;

    public bool Supports(GraphicsAPI api) => (_desc.supportedGraphicsAPIs & api) != 0;

    /// <summary>
    /// Every adapter NRI can see, in NRI's order: hardware first, as NRI sorts them. One native
    /// call, and not a cheap one: every unfiltered enumeration creates and destroys a Vulkan
    /// instance (the loader loads and unloads every implicit layer), creates a DXGI factory, runs a
    /// null-device <c>D3D12CreateDevice</c> probe per adapter and creates real inbox D3D12 devices
    /// (architecture and video-queue queries) for the adapters the Vulkan pass did not list, which
    /// leaves <c>d3d12.dll</c> and <c>D3D12Core.dll</c> loaded in the process. The count-then-fill
    /// pattern would pay that twice. To look at one backend's adapters only, use
    /// <see cref="Enumerate(GraphicsAPI)"/>. Hand the chosen adapter to
    /// <see cref="NriDeviceOptions.Adapter"/>; with none, <c>nriCreateDevice</c> enumerates on its
    /// own (for Vulkan the Vulkan pass only, patch 0024).
    /// </summary>
    public static IReadOnlyList<NriAdapter> Enumerate() => Enumerate(GraphicsAPI.ALL);

    /// <summary>
    /// The adapters of the backends in <paramref name="graphicsApis"/> (a mask), through
    /// <c>nriEnumerateAdapters</c> with that mask (NRI 962615b, #286): only those backends'
    /// enumeration passes run, so <c>Enumerate(GraphicsAPI.VK)</c> creates no DXGI factory and no
    /// D3D12 device. Same order as
    /// <see cref="Enumerate()"/> for the adapters both list. A filtered desc carries only the asked
    /// APIs' bits in <see cref="SupportedGraphicsApis"/> (plus <c>NONE</c>), so it cannot be handed
    /// to another API's device creation. For D3D11/D3D12 the desc comes from DXGI (queue counts a
    /// fixed 4 per type, DXGI's memory sizes); the unfiltered enumeration gives an adapter the
    /// Vulkan pass also lists the Vulkan pass's desc.
    /// </summary>
    public static unsafe IReadOnlyList<NriAdapter> Enumerate(GraphicsAPI graphicsApis)
    {
        // NRI itself sees at most 32 adapters (Creation.cpp), so the buffer is never short; it
        // returns min(count, found).
        var descs = new AdapterDesc[32];
        var count = (uint)descs.Length;
        Result enumerated;
        fixed (AdapterDesc* p = descs)
        {
            try { enumerated = NriApi.nriEnumerateAdapters(p, &count, graphicsApis); }
            catch (DllNotFoundException e) { throw NativeLibraryHint.Explain(e); } // the first call into NRI
        }
        NriException.Check(enumerated, "nriEnumerateAdapters");

        var result = new NriAdapter[count];
        for (var i = 0; i < count; i++) result[i] = new NriAdapter(in descs[i]);
        return result;
    }

    public override string ToString() => $"{Name} ({Vendor}, {Architecture}, {SupportedGraphicsApis})";
}
