namespace SharpNRI.Native;

/// <summary>Supported resolve ops of a resolve path</summary>
public partial struct ResolveOps
{
    /// <summary>non-integer color formats</summary>
    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits color;

    /// <summary>integer color formats</summary>
    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits colorInteger;

    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits depth;

    [NativeTypeName("nri::ResolveOpBits")]
    public ResolveOpBits stencil;
}
