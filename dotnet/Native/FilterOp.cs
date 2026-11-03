namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum FilterOp : byte
{
    AVERAGE,
    MIN,
    MAX,
    MAX_NUM,
}
