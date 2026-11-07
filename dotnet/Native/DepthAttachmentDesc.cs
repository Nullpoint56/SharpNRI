namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkPipelineDepthStencilStateCreateInfo.html</summary>
public partial struct DepthAttachmentDesc
{
    [NativeTypeName("nri::CompareOp")]
    public CompareOp compareOp;

    [NativeTypeName("bool")]
    public byte write;

    /// <summary>requires "features.depthBoundsTest", expects "CmdSetDepthBounds"</summary>
    [NativeTypeName("bool")]
    public byte boundsTest;
}
