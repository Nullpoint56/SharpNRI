namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum TextureType : byte
{
    TEXTURE_1D,
    TEXTURE_2D,
    TEXTURE_3D,
    MAX_NUM,
}
