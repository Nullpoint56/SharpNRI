namespace PrismNRI.Native;

public partial struct ColorAttachmentDesc
{
    [NativeTypeName("nri::Format")]
    public Format format;

    [NativeTypeName("nri::BlendDesc")]
    public BlendDesc colorBlend;

    [NativeTypeName("nri::BlendDesc")]
    public BlendDesc alphaBlend;

    [NativeTypeName("nri::ColorWriteBits")]
    public ColorWriteBits colorWriteMask;

    [NativeTypeName("bool")]
    public byte blendEnabled;
}
