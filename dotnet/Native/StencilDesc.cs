namespace PrismNRI.Native;

public partial struct StencilDesc
{
    [NativeTypeName("nri::CompareOp")]
    public CompareOp compareOp;

    [NativeTypeName("nri::StencilOp")]
    public StencilOp failOp;

    [NativeTypeName("nri::StencilOp")]
    public StencilOp passOp;

    [NativeTypeName("nri::StencilOp")]
    public StencilOp depthFailOp;

    [NativeTypeName("uint8_t")]
    public byte writeMask;

    [NativeTypeName("uint8_t")]
    public byte compareMask;
}
