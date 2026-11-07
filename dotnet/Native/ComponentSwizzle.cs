namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkComponentSwizzle.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_shader_component_mapping</summary>
[NativeTypeName("uint8_t")]
public enum ComponentSwizzle : byte
{
    /// <summary>format-specific default</summary>
    IDENTITY,
    /// <summary>Requires "features.componentSwizzle" 0</summary>
    ZERO,
    /// <summary>1 or 1.0</summary>
    ONE,
    /// <summary>x component (red)</summary>
    R,
    /// <summary>y component (green)</summary>
    G,
    /// <summary>z component (blue)</summary>
    B,
    /// <summary>w component (alpha)</summary>
    A,
    MAX_NUM,
}
