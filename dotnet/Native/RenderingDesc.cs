namespace SharpNRI.Native;

/// <summary>If "VK_KHR_dynamic_rendering" is not supported: - "VkRenderPass" is used under the hood - input attachments must be transitioned to "Layout::INPUT_ATTACHMENT" in the same command buffer before "CmdBeginRendering" - matching pipeline input attachment indices are inferred from these transitions</summary>
public unsafe partial struct RenderingDesc
{
    [NativeTypeName("const AttachmentDesc *")]
    public AttachmentDesc* colors;

    [NativeTypeName("uint32_t")]
    public uint colorNum;

    /// <summary>may be treated as "depth-stencil"</summary>
    [NativeTypeName("nri::AttachmentDesc")]
    public AttachmentDesc depth;

    /// <summary>(optional) separation is needed for multisample resolve, otherwise "depth.resolveOp" applies to the stencil plane</summary>
    [NativeTypeName("nri::AttachmentDesc")]
    public AttachmentDesc stencil;

    /// <summary>requires "tiers.shadingRate &gt;= 2"</summary>
    [NativeTypeName("const Descriptor *")]
    public Descriptor* shadingRate;

    /// <summary>if non-0, requires "viewMaxNum &gt; 1"</summary>
    [NativeTypeName("uint32_t")]
    public uint viewMask;
}
