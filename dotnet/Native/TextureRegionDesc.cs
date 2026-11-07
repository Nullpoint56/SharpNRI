namespace SharpNRI.Native;

/// <summary>Copy</summary>
public partial struct TextureRegionDesc
{
    [NativeTypeName("nri::Dim_t")]
    public ushort x;

    [NativeTypeName("nri::Dim_t")]
    public ushort y;

    [NativeTypeName("nri::Dim_t")]
    public ushort z;

    /// <summary>can be "WHOLE_SIZE" (mip)</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    /// <summary>can be "WHOLE_SIZE" (mip)</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort height;

    /// <summary>can be "WHOLE_SIZE" (mip)</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort depth;

    [NativeTypeName("nri::Dim_t")]
    public ushort mipOffset;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerOffset;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;
}
