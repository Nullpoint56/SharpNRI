namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/guide/latest/robustness.html</summary>
[NativeTypeName("uint8_t")]
public enum Robustness : byte
{
    /// <summary>don't care, follow device settings (VK level when used on a device)</summary>
    DEFAULT,
    /// <summary>no overhead, no robust access (out-of-bounds access is not allowed)</summary>
    OFF,
    /// <summary>minimal overhead, partial robust access</summary>
    VK,
    /// <summary>moderate overhead, D3D12-level robust access (requires "VK_EXT_robustness2", soft fallback to VK mode)</summary>
    D3D12,
    MAX_NUM,
}
