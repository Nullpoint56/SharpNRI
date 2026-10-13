namespace PrismNRI.Native;

public partial struct MicromapUsageDesc
{
    [NativeTypeName("uint32_t")]
    public uint triangleNum;

    [NativeTypeName("uint16_t")]
    public ushort subdivisionLevel;

    [NativeTypeName("nri::MicromapFormat")]
    public MicromapFormat format;
}
