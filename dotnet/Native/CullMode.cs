namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkCullModeFlagBits.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_cull_mode</summary>
[NativeTypeName("uint8_t")]
public enum CullMode : byte
{
    NONE,
    FRONT,
    BACK,
    MAX_NUM,
}
