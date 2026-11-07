namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkStencilOp.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_stencil_op R - reference, set by "CmdSetStencilReference" D - stencil buffer</summary>
[NativeTypeName("uint8_t")]
public enum StencilOp : byte
{
    /// <summary>D = D</summary>
    KEEP,
    /// <summary>D = 0</summary>
    ZERO,
    /// <summary>D = R</summary>
    REPLACE,
    /// <summary>D = min(D++, 255)</summary>
    INCREMENT_AND_CLAMP,
    /// <summary>D = max(D--, 0)</summary>
    DECREMENT_AND_CLAMP,
    /// <summary>D = ~D</summary>
    INVERT,
    /// <summary>D++</summary>
    INCREMENT_AND_WRAP,
    /// <summary>D--</summary>
    DECREMENT_AND_WRAP,
    MAX_NUM,
}
