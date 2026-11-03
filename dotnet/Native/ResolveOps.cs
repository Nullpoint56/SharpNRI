namespace SharpNRI.Native;

public partial struct ResolveOps
{
    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits color;

    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits colorInteger;

    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits depth;

    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits stencil;
}
