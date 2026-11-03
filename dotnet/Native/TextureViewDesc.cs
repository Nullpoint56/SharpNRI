namespace PrismNRI.Native;

public unsafe partial struct TextureViewDesc
{
    [NativeTypeName("const Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::TextureView")]
    public TextureView type;

    [NativeTypeName("nri::Format")]
    public Format format;

    [NativeTypeName("nri::Dim_t")]
    public ushort mipOffset;

    [NativeTypeName("nri::Dim_t")]
    public ushort mipNum;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerOffset;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerNum;

    [NativeTypeName("nri::Dim_t")]
    public ushort sliceOffset;

    [NativeTypeName("nri::Dim_t")]
    public ushort sliceNum;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;

    [NativeTypeName("nri::ComponentMapping")]
    public ComponentMapping components;

    [NativeTypeName("nri::Layout")]
    public Layout layout;
}
