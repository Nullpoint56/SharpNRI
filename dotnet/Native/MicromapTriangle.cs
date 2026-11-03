namespace SharpNRI.Native;

public partial struct MicromapTriangle
{
    [NativeTypeName("uint32_t")]
    public uint dataOffset;

    [NativeTypeName("uint16_t")]
    public ushort subdivisionLevel;

    [NativeTypeName("nri::MicromapFormat")]
    public MicromapFormat format;
}
