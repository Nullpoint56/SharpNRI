namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum LogicOp : byte
{
    NONE,
    CLEAR,
    AND,
    AND_REVERSE,
    COPY,
    AND_INVERTED,
    XOR,
    OR,
    NOR,
    EQUIVALENT,
    INVERT,
    OR_REVERSE,
    COPY_INVERTED,
    OR_INVERTED,
    NAND,
    SET,
    MAX_NUM,
}
