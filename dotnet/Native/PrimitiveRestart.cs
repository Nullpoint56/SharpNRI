namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum PrimitiveRestart : byte
{
    DISABLED,
    INDICES_UINT16,
    INDICES_UINT32,
    MAX_NUM,
}
