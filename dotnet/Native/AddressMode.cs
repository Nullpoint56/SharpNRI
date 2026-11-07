namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkSamplerAddressMode.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_texture_address_mode</summary>
[NativeTypeName("uint8_t")]
public enum AddressMode : byte
{
    REPEAT,
    MIRRORED_REPEAT,
    CLAMP_TO_EDGE,
    /// <summary>WGPU: unsupported</summary>
    CLAMP_TO_BORDER,
    MIRROR_CLAMP_TO_EDGE,
    MAX_NUM,
}
