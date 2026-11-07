namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkStencilOpState.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_depth_stencil_desc</summary>
public partial struct StencilDesc
{
    /// <summary>"compareOp != NONE", expects "CmdSetStencilReference"</summary>
    [NativeTypeName("nri::CompareOp")]
    public CompareOp compareOp;

    [NativeTypeName("nri::StencilOp")]
    public StencilOp failOp;

    [NativeTypeName("nri::StencilOp")]
    public StencilOp passOp;

    [NativeTypeName("nri::StencilOp")]
    public StencilOp depthFailOp;

    [NativeTypeName("uint8_t")]
    public byte writeMask;

    [NativeTypeName("uint8_t")]
    public byte compareMask;
}
