namespace SharpNRI.Native;

/// <summary>https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_filter_reduction_type https://docs.vulkan.org/refpages/latest/refpages/source/VkSamplerReductionMode.html</summary>
[NativeTypeName("uint8_t")]
public enum FilterOp : byte
{
    /// <summary>a weighted average (sum) of values in the footprint (default)</summary>
    AVERAGE,
    /// <summary>a component-wise minimum of values in the footprint with non-zero weights, requires "features.filterOpMinMax"</summary>
    MIN,
    /// <summary>a component-wise maximum of values in the footprint with non-zero weights, requires "features.filterOpMinMax"</summary>
    MAX,
    MAX_NUM,
}
