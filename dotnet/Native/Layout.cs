namespace SharpNRI.Native;

/// <summary>Without "features.enhancedBarriers", "Layout" in barriers is used only for attachment resolve states https://docs.vulkan.org/refpages/latest/refpages/source/VkImageLayout.html https://microsoft.github.io/DirectX-Specs/d3d/D3D12EnhancedBarriers.html#d3d12_barrier_layout</summary>
[NativeTypeName("uint8_t")]
public enum Layout : byte
{
    /// <summary>Special https://microsoft.github.io/DirectX-Specs/d3d/D3D12EnhancedBarriers.html#d3d12_barrier_layout_undefined</summary>
    UNDEFINED,
    /// <summary>ALL access, required for "SharingMode::SIMULTANEOUS" (but may be suboptimal if "features.unifiedTextureLayouts" is not supported)</summary>
    GENERAL,
    /// <summary>NONE (use "after.stages = StageBits::NONE")</summary>
    PRESENT,
    /// <summary>Attachment Color and depth-stencil attachment layouts also support "RESOLVE_ATTACHMENT_SOURCE" access, writable planes support "RESOLVE_ATTACHMENT_DESTINATION" access COLOR_ATTACHMENT_READ/WRITE</summary>
    COLOR_ATTACHMENT,
    /// <summary>DEPTH_STENCIL_ATTACHMENT_READ/WRITE</summary>
    DEPTH_STENCIL_ATTACHMENT,
    /// <summary>DEPTH_STENCIL_ATTACHMENT_READ/WRITE (only stencil writable), SHADER_RESOURCE (depth plane)</summary>
    DEPTH_READONLY_STENCIL_ATTACHMENT,
    /// <summary>DEPTH_STENCIL_ATTACHMENT_READ/WRITE (only depth writable), SHADER_RESOURCE (stencil plane)</summary>
    DEPTH_ATTACHMENT_STENCIL_READONLY,
    /// <summary>DEPTH_STENCIL_ATTACHMENT_READ, SHADER_RESOURCE (D3D12: graphics queue only)</summary>
    DEPTH_STENCIL_READONLY,
    /// <summary>SHADING_RATE_ATTACHMENT</summary>
    SHADING_RATE_ATTACHMENT,
    /// <summary>COLOR_ATTACHMENT, INPUT_ATTACHMENT</summary>
    INPUT_ATTACHMENT,
    /// <summary>Shader SHADER_RESOURCE</summary>
    SHADER_RESOURCE,
    /// <summary>SHADER_RESOURCE_STORAGE</summary>
    SHADER_RESOURCE_STORAGE,
    /// <summary>Copy COPY_SOURCE</summary>
    COPY_SOURCE,
    /// <summary>COPY_DESTINATION</summary>
    COPY_DESTINATION,
    /// <summary>Resolve RESOLVE_SOURCE</summary>
    RESOLVE_SOURCE,
    /// <summary>RESOLVE_DESTINATION</summary>
    RESOLVE_DESTINATION,
    /// <summary>Video VIDEO_DECODE_WRITE</summary>
    VIDEO_DECODE_DST,
    /// <summary>VIDEO_DECODE_READ/WRITE</summary>
    VIDEO_DECODE_DPB,
    /// <summary>VIDEO_ENCODE_READ</summary>
    VIDEO_ENCODE_SRC,
    /// <summary>VIDEO_ENCODE_READ/WRITE</summary>
    VIDEO_ENCODE_DPB,
    MAX_NUM,
}
