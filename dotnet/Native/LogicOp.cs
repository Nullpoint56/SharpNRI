namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkLogicOp.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_logic_op S - source color 0 D - destination color</summary>
[NativeTypeName("uint8_t")]
public enum LogicOp : byte
{
    NONE,
    /// <summary>0</summary>
    CLEAR,
    /// <summary>S &amp; D</summary>
    AND,
    /// <summary>S &amp; ~D</summary>
    AND_REVERSE,
    /// <summary>S</summary>
    COPY,
    /// <summary>~S &amp; D</summary>
    AND_INVERTED,
    /// <summary>S ^ D</summary>
    XOR,
    /// <summary>S | D</summary>
    OR,
    /// <summary>~(S | D)</summary>
    NOR,
    /// <summary>~(S ^ D)</summary>
    EQUIVALENT,
    /// <summary>~D</summary>
    INVERT,
    /// <summary>S | ~D</summary>
    OR_REVERSE,
    /// <summary>~S</summary>
    COPY_INVERTED,
    /// <summary>~S | D</summary>
    OR_INVERTED,
    /// <summary>~(S &amp; D)</summary>
    NAND,
    /// <summary>1</summary>
    SET,
    MAX_NUM,
}
