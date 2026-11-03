namespace SharpNRI.Native;

public unsafe partial struct OutputMergerDesc
{
    [NativeTypeName("const ColorAttachmentDesc *")]
    public ColorAttachmentDesc* colors;

    [NativeTypeName("uint32_t")]
    public uint colorNum;

    [NativeTypeName("nri::DepthAttachmentDesc")]
    public DepthAttachmentDesc depth;

    [NativeTypeName("nri::StencilAttachmentDesc")]
    public StencilAttachmentDesc stencil;

    [NativeTypeName("nri::Format")]
    public Format depthStencilFormat;

    [NativeTypeName("nri::LogicOp")]
    public LogicOp logicOp;

    [NativeTypeName("uint32_t")]
    public uint viewMask;

    [NativeTypeName("nri::Multiview")]
    public Multiview multiview;
}
