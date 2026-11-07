namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkPipelineColorBlendAttachmentState.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_render_target_blend_desc</summary>
public partial struct BlendDesc
{
    [NativeTypeName("nri::BlendFactor")]
    public BlendFactor srcFactor;

    [NativeTypeName("nri::BlendFactor")]
    public BlendFactor dstFactor;

    [NativeTypeName("nri::BlendOp")]
    public BlendOp op;
}
