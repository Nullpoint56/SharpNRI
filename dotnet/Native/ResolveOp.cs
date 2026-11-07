namespace SharpNRI.Native;

/// <summary>https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_resolve_mode https://docs.vulkan.org/refpages/latest/refpages/source/VkResolveModeFlagBits.html Supported resolve ops are reported per path and plane, see "DeviceDesc::resolve"</summary>
[NativeTypeName("uint8_t")]
public enum ResolveOp : byte
{
    /// <summary>resolves the source samples to their average value (can't be used with integer and stencil formats)</summary>
    AVERAGE,
    /// <summary>resolves the source samples to their minimum value</summary>
    MIN,
    /// <summary>resolves the source samples to their maximum value</summary>
    MAX,
    /// <summary>takes the value of the sample 0</summary>
    SAMPLE_ZERO,
    MAX_NUM,
}
