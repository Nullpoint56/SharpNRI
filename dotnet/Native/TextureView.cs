namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum TextureView : byte
{
    /// <summary>Shader resources // HLSL type Compatible "DescriptorType" Compatible "TextureType" Texture[1D/2D/3D](MS) TEXTURE 1D, 2D, 3D</summary>
    TEXTURE,
    /// <summary>Texture[1D/2D](MS)Array TEXTURE 1D, 2D</summary>
    TEXTURE_ARRAY,
    /// <summary>TextureCube TEXTURE 2D</summary>
    TEXTURE_CUBE,
    /// <summary>TextureCubeArray TEXTURE 2D</summary>
    TEXTURE_CUBE_ARRAY,
    /// <summary>RWTexture[1D/2D/3D](MS) STORAGE_TEXTURE 1D, 2D, 3D</summary>
    STORAGE_TEXTURE,
    /// <summary>RWTexture[1D/2D](MS)Array STORAGE_TEXTURE 1D, 2D</summary>
    STORAGE_TEXTURE_ARRAY,
    /// <summary>SubpassInput(MS) (non-array) INPUT_ATTACHMENT 2D</summary>
    SUBPASS_INPUT,
    /// <summary>Host-only 1D, 2D, 3D</summary>
    COLOR_ATTACHMENT,
    /// <summary>1D, 2D</summary>
    DEPTH_STENCIL_ATTACHMENT,
    /// <summary>2D</summary>
    SHADING_RATE_ATTACHMENT,
    MAX_NUM,
}
