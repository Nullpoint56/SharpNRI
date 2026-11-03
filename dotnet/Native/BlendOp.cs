namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum BlendOp : byte
{
    ADD,
    SUBTRACT,
    REVERSE_SUBTRACT,
    MIN,
    MAX,
    MAX_NUM,
}
