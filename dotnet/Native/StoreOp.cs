namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum StoreOp : byte
{
    STORE,
    DISCARD,
    NONE,
    MAX_NUM,
}
