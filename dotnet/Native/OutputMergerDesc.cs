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

    /// <summary>requires "features.logicOp"</summary>
    [NativeTypeName("nri::LogicOp")]
    public LogicOp logicOp;

    /// <summary>if non-0, requires "viewMaxNum &gt; 1"</summary>
    [NativeTypeName("uint32_t")]
    public uint viewMask;

    /// <summary>if "viewMask != 0", requires "features.(xxx)Multiview"</summary>
    [NativeTypeName("nri::Multiview")]
    public Multiview multiview;
}
