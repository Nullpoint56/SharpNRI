namespace PrismNRI.Native;

public unsafe partial struct FormatProps
{
    [NativeTypeName("const char *")]
    public sbyte* name;

    [NativeTypeName("nri::Format")]
    public Format format;

    [NativeTypeName("uint8_t")]
    public byte redBits;

    [NativeTypeName("uint8_t")]
    public byte greenBits;

    [NativeTypeName("uint8_t")]
    public byte blueBits;

    [NativeTypeName("uint8_t")]
    public byte alphaBits;

    [NativeTypeName("uint8_t")]
    public byte stride;

    [NativeTypeName("uint8_t")]
    public byte blockWidth;

    [NativeTypeName("uint8_t")]
    public byte blockHeight;

    [NativeTypeName("bool")]
    public byte isBgr;

    [NativeTypeName("bool")]
    public byte isCompressed;

    [NativeTypeName("bool")]
    public byte isDepth;

    [NativeTypeName("bool")]
    public byte isExpShared;

    [NativeTypeName("bool")]
    public byte isFloat;

    [NativeTypeName("bool")]
    public byte isPacked;

    [NativeTypeName("bool")]
    public byte isInteger;

    [NativeTypeName("bool")]
    public byte isNorm;

    [NativeTypeName("bool")]
    public byte isSigned;

    [NativeTypeName("bool")]
    public byte isSrgb;

    [NativeTypeName("bool")]
    public byte isStencil;
}
