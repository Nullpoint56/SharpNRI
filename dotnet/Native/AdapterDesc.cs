using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct AdapterDesc
{
    [NativeTypeName("char[256]")]
    public _name_e__FixedBuffer name;

    /// <summary>"LUID" (preferred) if "uid.high = 0", or "UUID" otherwise</summary>
    [NativeTypeName("nri::Uid_t")]
    public Uid_t uid;

    [NativeTypeName("uint64_t")]
    public ulong videoMemorySize;

    [NativeTypeName("uint64_t")]
    public ulong sharedSystemMemorySize;

    [NativeTypeName("uint32_t")]
    public uint deviceId;

    /// <summary>GAPI and OS dependent</summary>
    [NativeTypeName("uint32_t")]
    public uint driverVersion;

    /// <summary>per type; queues of different types may alias the same native queue</summary>
    [NativeTypeName("uint32_t[5]")]
    public _queueNum_e__FixedBuffer queueNum;

    [NativeTypeName("nri::Vendor")]
    public Vendor vendor;

    [NativeTypeName("nri::Architecture")]
    public Architecture architecture;

    [NativeTypeName("nri::GraphicsAPI")]
    public GraphicsAPI supportedGraphicsAPIs;

    [InlineArray(256)]
    public partial struct _name_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(5)]
    public partial struct _queueNum_e__FixedBuffer
    {
        public uint e0;
    }
}
