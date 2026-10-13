// L2 — adapters, before there is a device.
//
// nriEnumerateAdapters is a two-call pattern over AdapterDesc. The desc is kept whole because
// nriCreateDevice takes a pointer to it to select the adapter; the properties are the same bytes
// read out by name.

using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

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

    /// <summary>Every adapter NRI can see, in NRI's order: hardware first, as NRI sorts them.</summary>
    public static unsafe IReadOnlyList<NriAdapter> Enumerate()
    {
        uint count = 0;
        NriException.Check(NriApi.nriEnumerateAdapters(null, &count), "nriEnumerateAdapters");
        if (count == 0) return [];

        var descs = new AdapterDesc[count];
        fixed (AdapterDesc* p = descs)
        {
            NriException.Check(NriApi.nriEnumerateAdapters(p, &count), "nriEnumerateAdapters");
        }

        var result = new NriAdapter[count];
        for (var i = 0; i < count; i++) result[i] = new NriAdapter(in descs[i]);
        return result;
    }

    public override string ToString() => $"{Name} ({Vendor}, {Architecture}, {SupportedGraphicsApis})";
}
