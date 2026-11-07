namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkBlendOp.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_blend_op S - source color D - destination color Sf - source factor, produced by "BlendFactor" Df - destination factor, produced by "BlendFactor"</summary>
[NativeTypeName("uint8_t")]
public enum BlendOp : byte
{
    /// <summary>S * Sf + D * Df</summary>
    ADD,
    /// <summary>S * Sf - D * Df</summary>
    SUBTRACT,
    /// <summary>D * Df - S * Sf</summary>
    REVERSE_SUBTRACT,
    /// <summary>min(S, D)</summary>
    MIN,
    /// <summary>max(S, D)</summary>
    MAX,
    MAX_NUM,
}
