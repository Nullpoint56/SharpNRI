namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum SharedHandleType : byte
{
    NONE,
    OPAQUE_WIN32,
    OPAQUE_FD,
    D3D12_RESOURCE,
    D3D12_FENCE,
    MAX_NUM,
}
