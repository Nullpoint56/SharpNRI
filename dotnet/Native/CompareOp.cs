namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkCompareOp.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_comparison_func R - fragment depth, stencil reference or "SampleCmp" reference D - depth or stencil buffer</summary>
[NativeTypeName("uint8_t")]
public enum CompareOp : byte
{
    /// <summary>test is disabled</summary>
    NONE,
    /// <summary>true</summary>
    ALWAYS,
    /// <summary>false</summary>
    NEVER,
    /// <summary>R == D</summary>
    EQUAL,
    /// <summary>R != D</summary>
    NOT_EQUAL,
    /// <summary>R &lt; D</summary>
    LESS,
    /// <summary>R &lt;= D</summary>
    LESS_EQUAL,
    /// <summary>R &gt; D</summary>
    GREATER,
    /// <summary>R &gt;= D</summary>
    GREATER_EQUAL,
    MAX_NUM,
}
