namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum PrimitiveRestart : byte
{
    DISABLED,
    /// <summary>index "0xFFFF" enforces primitive restart</summary>
    INDICES_UINT16,
    /// <summary>index "0xFFFFFFFF" enforces primitive restart</summary>
    INDICES_UINT32,
    MAX_NUM,
}
