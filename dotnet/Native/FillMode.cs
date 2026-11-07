namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkPolygonMode.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_fill_mode</summary>
[NativeTypeName("uint8_t")]
public enum FillMode : byte
{
    SOLID,
    WIREFRAME,
    MAX_NUM,
}
