namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum CullMode : byte
{
    NONE,
    FRONT,
    BACK,
    MAX_NUM,
}
