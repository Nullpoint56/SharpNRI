namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum BufferView : byte
{
    BUFFER,
    STRUCTURED_BUFFER,
    BYTE_ADDRESS_BUFFER,
    STORAGE_BUFFER,
    STORAGE_STRUCTURED_BUFFER,
    STORAGE_BYTE_ADDRESS_BUFFER,
    CONSTANT_BUFFER,
    MAX_NUM,
}
