namespace SharpNRI.Native;

public partial struct MicromapUsageDesc
{
    /// <summary>represents "MicromapTriangle" number for "{format, subdivisionLevel}" pair contained in the micromap</summary>
    [NativeTypeName("uint32_t")]
    public uint triangleNum;

    /// <summary>micro triangles count = 4 ^ subdivisionLevel</summary>
    [NativeTypeName("uint16_t")]
    public ushort subdivisionLevel;

    [NativeTypeName("nri::MicromapFormat")]
    public MicromapFormat format;
}
