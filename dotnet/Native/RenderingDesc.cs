namespace SharpNRI.Native;

public unsafe partial struct RenderingDesc
{
    [NativeTypeName("const AttachmentDesc *")]
    public AttachmentDesc* colors;

    [NativeTypeName("uint32_t")]
    public uint colorNum;

    [NativeTypeName("nri::AttachmentDesc")]
    public AttachmentDesc depth;

    [NativeTypeName("nri::AttachmentDesc")]
    public AttachmentDesc stencil;

    [NativeTypeName("const Descriptor *")]
    public Descriptor* shadingRate;

    [NativeTypeName("uint32_t")]
    public uint viewMask;
}
