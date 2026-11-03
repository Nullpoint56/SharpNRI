namespace SharpNRI.Native;

public partial struct BlendDesc
{
    [NativeTypeName("nri::BlendFactor")]
    public BlendFactor srcFactor;

    [NativeTypeName("nri::BlendFactor")]
    public BlendFactor dstFactor;

    [NativeTypeName("nri::BlendOp")]
    public BlendOp op;
}
