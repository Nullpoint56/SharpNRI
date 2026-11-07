namespace SharpNRI.Native;

public unsafe partial struct FormatProps
{
    /// <summary>format name</summary>
    [NativeTypeName("const char *")]
    public sbyte* name;

    /// <summary>self</summary>
    [NativeTypeName("nri::Format")]
    public Format format;

    /// <summary>R (or depth) bits</summary>
    [NativeTypeName("uint8_t")]
    public byte redBits;

    /// <summary>G (or stencil) bits (0 if channels &lt; 2)</summary>
    [NativeTypeName("uint8_t")]
    public byte greenBits;

    /// <summary>B bits (0 if channels &lt; 3)</summary>
    [NativeTypeName("uint8_t")]
    public byte blueBits;

    /// <summary>A (or shared exponent) bits (0 if channels &lt; 4)</summary>
    [NativeTypeName("uint8_t")]
    public byte alphaBits;

    /// <summary>block size in bytes</summary>
    [NativeTypeName("uint8_t")]
    public byte stride;

    /// <summary>1 for plain formats, &gt;1 for compressed</summary>
    [NativeTypeName("uint8_t")]
    public byte blockWidth;

    /// <summary>1 for plain formats, &gt;1 for compressed</summary>
    [NativeTypeName("uint8_t")]
    public byte blockHeight;

    /// <summary>reversed channels (RGBA =&gt; BGRA)</summary>
    [NativeTypeName("bool")]
    public byte isBgr;

    /// <summary>block-compressed format</summary>
    [NativeTypeName("bool")]
    public byte isCompressed;

    /// <summary>has depth component</summary>
    [NativeTypeName("bool")]
    public byte isDepth;

    /// <summary>shared exponent in alpha channel</summary>
    [NativeTypeName("bool")]
    public byte isExpShared;

    /// <summary>floating point</summary>
    [NativeTypeName("bool")]
    public byte isFloat;

    /// <summary>16- or 32- bit packed</summary>
    [NativeTypeName("bool")]
    public byte isPacked;

    /// <summary>integer</summary>
    [NativeTypeName("bool")]
    public byte isInteger;

    /// <summary>[0; 1] normalized</summary>
    [NativeTypeName("bool")]
    public byte isNorm;

    /// <summary>signed</summary>
    [NativeTypeName("bool")]
    public byte isSigned;

    /// <summary>sRGB</summary>
    [NativeTypeName("bool")]
    public byte isSrgb;

    /// <summary>has stencil component</summary>
    [NativeTypeName("bool")]
    public byte isStencil;
}
