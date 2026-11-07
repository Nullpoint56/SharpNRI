namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkFilter.html https://docs.vulkan.org/refpages/latest/refpages/source/VkSamplerMipmapMode.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_filter</summary>
[NativeTypeName("uint8_t")]
public enum Filter : byte
{
    NEAREST,
    LINEAR,
    MAX_NUM,
}
