namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkBlendFactor.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_blend S0 - source color 0 S1 - source color 1 D - destination color C - blend constants, set by "CmdSetBlendConstants"</summary>
[NativeTypeName("uint8_t")]
public enum BlendFactor : byte
{
    /// <summary>0 0</summary>
    ZERO,
    /// <summary>1 1</summary>
    ONE,
    /// <summary>S0.r, S0.g, S0.b S0.a</summary>
    SRC_COLOR,
    /// <summary>1 - S0.r, 1 - S0.g, 1 - S0.b 1 - S0.a</summary>
    ONE_MINUS_SRC_COLOR,
    /// <summary>D.r, D.g, D.b D.a</summary>
    DST_COLOR,
    /// <summary>1 - D.r, 1 - D.g, 1 - D.b 1 - D.a</summary>
    ONE_MINUS_DST_COLOR,
    /// <summary>S0.a S0.a</summary>
    SRC_ALPHA,
    /// <summary>1 - S0.a 1 - S0.a</summary>
    ONE_MINUS_SRC_ALPHA,
    /// <summary>D.a D.a</summary>
    DST_ALPHA,
    /// <summary>1 - D.a 1 - D.a</summary>
    ONE_MINUS_DST_ALPHA,
    /// <summary>C.r, C.g, C.b C.a</summary>
    CONSTANT_COLOR,
    /// <summary>1 - C.r, 1 - C.g, 1 - C.b 1 - C.a</summary>
    ONE_MINUS_CONSTANT_COLOR,
    /// <summary>C.a C.a (for RGB requires "features.constantAlphaBlendFactors")</summary>
    CONSTANT_ALPHA,
    /// <summary>1 - C.a 1 - C.a (for RGB requires "features.constantAlphaBlendFactors")</summary>
    ONE_MINUS_CONSTANT_ALPHA,
    /// <summary>min(S0.a, 1 - D.a) 1</summary>
    SRC_ALPHA_SATURATE,
    /// <summary>S1.r, S1.g, S1.b S1.a</summary>
    SRC1_COLOR,
    /// <summary>1 - S1.r, 1 - S1.g, 1 - S1.b 1 - S1.a</summary>
    ONE_MINUS_SRC1_COLOR,
    /// <summary>S1.a S1.a</summary>
    SRC1_ALPHA,
    /// <summary>1 - S1.a 1 - S1.a</summary>
    ONE_MINUS_SRC1_ALPHA,
    MAX_NUM,
}
