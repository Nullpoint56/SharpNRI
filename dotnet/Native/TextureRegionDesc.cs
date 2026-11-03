namespace SharpNRI.Native;

public partial struct TextureRegionDesc
{
    [NativeTypeName("nri::Dim_t")]
    public ushort x;

    [NativeTypeName("nri::Dim_t")]
    public ushort y;

    [NativeTypeName("nri::Dim_t")]
    public ushort z;

    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    [NativeTypeName("nri::Dim_t")]
    public ushort height;

    [NativeTypeName("nri::Dim_t")]
    public ushort depth;

    [NativeTypeName("nri::Dim_t")]
    public ushort mipOffset;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerOffset;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;
}
