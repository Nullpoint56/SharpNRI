namespace SharpNRI.Native;

/// <summary>A bit represents a feature, supported by a format https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_feature_data_format_support https://docs.vulkan.org/refpages/latest/refpages/source/VkFormatFeatureFlagBits2.html WGPU: typed buffer views are unsupported; storage textures cannot be multisampled</summary>
[NativeTypeName("uint32_t")]
[Flags]
public enum FormatSupportBits : uint
{
    /// <summary>format is unsupported</summary>
    UNSUPPORTED = 0,
    /// <summary>Texture sampled texture view</summary>
    TEXTURE = (1U << (0)),
    /// <summary>storage texture view</summary>
    STORAGE_TEXTURE = (1U << (1)),
    /// <summary>storage texture atomics other than Load / Store</summary>
    STORAGE_TEXTURE_ATOMICS = (1U << (2)),
    /// <summary>color attachment view</summary>
    COLOR_ATTACHMENT = (1U << (3)),
    /// <summary>depth-stencil attachment view</summary>
    DEPTH_STENCIL_ATTACHMENT = (1U << (4)),
    /// <summary>color attachment blending</summary>
    BLEND = (1U << (5)),
    /// <summary>2x multisampled texture</summary>
    MULTISAMPLE_2X = (1U << (6)),
    /// <summary>4x multisampled texture</summary>
    MULTISAMPLE_4X = (1U << (7)),
    /// <summary>8x multisampled texture</summary>
    MULTISAMPLE_8X = (1U << (8)),
    /// <summary>resolve source/destination</summary>
    MULTISAMPLE_RESOLVE = (1U << (9)),
    /// <summary>Buffer typed buffer view</summary>
    BUFFER = (1U << (10)),
    /// <summary>typed storage buffer view</summary>
    STORAGE_BUFFER = (1U << (11)),
    /// <summary>typed storage buffer atomics other than Load / Store</summary>
    STORAGE_BUFFER_ATOMICS = (1U << (12)),
    /// <summary>vertex buffer attribute</summary>
    VERTEX_BUFFER = (1U << (13)),
    /// <summary>Texture / buffer storage read with unknown format</summary>
    STORAGE_READ_WITHOUT_FORMAT = (1U << (14)),
    /// <summary>storage write with unknown format</summary>
    STORAGE_WRITE_WITHOUT_FORMAT = (1U << (15)),
    /// <summary>Host (generally supported for non-depth/stencil formats with "TEXTURE" bit support) synchronous host copies are supported</summary>
    HOST_COPY = (1U << (16)),
}
