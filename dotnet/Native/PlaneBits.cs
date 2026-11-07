namespace SharpNRI.Native;

/// <summary>https://learn.microsoft.com/en-us/windows/win32/direct3d12/subresources#plane-slice https://docs.vulkan.org/refpages/latest/refpages/source/VkImageAspectFlagBits.html</summary>
[NativeTypeName("uint8_t")]
[Flags]
public enum PlaneBits : byte
{
    /// <summary>lazy default</summary>
    ALL = 0,
    /// <summary>no accessible planes (needed for a read-only depth-stencil attachment)</summary>
    NONE = (1 << 7),
    /// <summary>indicates "color" plane (same as "ALL" for color formats)</summary>
    COLOR = (1 << 0),
    /// <summary>D3D11: can't be addressed individually in "copy" and "resolve" operations indicates "depth" plane (same as "ALL" for depth-only formats)</summary>
    DEPTH = (1 << 1),
    /// <summary>indicates "stencil" plane in depth-stencil formats</summary>
    STENCIL = (1 << 2),
    /// <summary>For multi-planar YUV formats</summary>
    PLANE_0 = (1 << 3),
    PLANE_1 = (1 << 4),
    PLANE_2 = (1 << 5),
    /// <summary>Aliases</summary>
    PLANE_Y = PLANE_0,
    PLANE_UV = PLANE_1,
}
