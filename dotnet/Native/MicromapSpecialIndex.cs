namespace SharpNRI.Native;

[NativeTypeName("int8_t")]
public enum MicromapSpecialIndex : sbyte
{
    FULLY_TRANSPARENT = -1,
    FULLY_OPAQUE = -2,
    FULLY_UNKNOWN_TRANSPARENT = -3,
    FULLY_UNKNOWN_OPAQUE = -4,
    MAX_NUM,
}
