namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkFragmentShadingRateCombinerOpKHR.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_shading_rate_combiner "primitiveCombiner" "attachmentCombiner" A Pipeline shading rate Result of Op1 B Primitive shading rate Attachment shading rate</summary>
[NativeTypeName("uint8_t")]
public enum ShadingRateCombiner : byte
{
    /// <summary>A</summary>
    KEEP,
    /// <summary>Requires "tiers.shadingRate &gt;= 2" B</summary>
    REPLACE,
    /// <summary>min(A, B)</summary>
    MIN,
    /// <summary>max(A, B)</summary>
    MAX,
    /// <summary>Requires "features.sumShadingRateCombiner" (A + B) or (A * B)</summary>
    SUM,
    MAX_NUM,
}
