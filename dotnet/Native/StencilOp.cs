namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum StencilOp : byte
{
    KEEP,
    ZERO,
    REPLACE,
    INCREMENT_AND_CLAMP,
    DECREMENT_AND_CLAMP,
    INVERT,
    INCREMENT_AND_WRAP,
    DECREMENT_AND_WRAP,
    MAX_NUM,
}
