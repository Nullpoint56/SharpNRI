namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum CompareOp : byte
{
    NONE,
    ALWAYS,
    NEVER,
    EQUAL,
    NOT_EQUAL,
    LESS,
    LESS_EQUAL,
    GREATER,
    GREATER_EQUAL,
    MAX_NUM,
}
