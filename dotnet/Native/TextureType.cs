namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkImageType.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_resource_dimension</summary>
[NativeTypeName("uint8_t")]
public enum TextureType : byte
{
    /// <summary>WGPU: arrays and mipmaps are unsupported; METAL: a 2D texture with "height = 1" ("texture2d" in native shaders)</summary>
    TEXTURE_1D,
    TEXTURE_2D,
    /// <summary>arrays are unsupported</summary>
    TEXTURE_3D,
    MAX_NUM,
}
