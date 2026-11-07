namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkImageUsageFlagBits.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_resource_flags</summary>
[NativeTypeName("uint16_t")]
[Flags]
public enum TextureUsageBits : ushort
{
    NONE = 0,
    /// <summary>SHADER_RESOURCE Read-only shader resource view (SRV)</summary>
    SHADER_RESOURCE = (1 << 0),
    /// <summary>SHADER_RESOURCE_STORAGE Read/write shader resource view (UAV)</summary>
    SHADER_RESOURCE_STORAGE = (1 << 1),
    /// <summary>COLOR_ATTACHMENT Color attachment (render target)</summary>
    COLOR_ATTACHMENT = (1 << 2),
    /// <summary>DEPTH_STENCIL_ATTACHMENT_READ/WRITE Depth-stencil attachment (depth-stencil target)</summary>
    DEPTH_STENCIL_ATTACHMENT = (1 << 3),
    /// <summary>SHADING_RATE_ATTACHMENT Shading rate attachment (source)</summary>
    SHADING_RATE_ATTACHMENT = (1 << 4),
    /// <summary>INPUT_ATTACHMENT Subpass input (read on-chip tile cache)</summary>
    INPUT_ATTACHMENT = (1 << 5),
    /// <summary>HOST_READ/HOST_WRITE Synchronous copy between texture and host memory</summary>
    HOST_TRANSFER = (1 << 6),
    /// <summary>VIDEO_DECODE Video decode output / DPB picture</summary>
    VIDEO_DECODE = (1 << 7),
    /// <summary>VIDEO_ENCODE Video encode input / DPB picture</summary>
    VIDEO_ENCODE = (1 << 8),
    /// <summary>VIDEO_* Video DPB/reference-only allocation</summary>
    VIDEO_REFERENCE_ONLY = (1 << 9),
    /// <summary>VIDEO_* Video decode output / encode input never used as a DPB picture (required if "decodeDpbAndOutputCoincide" is false; encode: pair with "VIDEO_REFERENCE_ONLY" DPB textures, VK may not support the combined usage)</summary>
    VIDEO_OUTPUT_ONLY = (1 << 10),
}
