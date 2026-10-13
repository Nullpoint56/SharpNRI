namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum MemoryLocation : byte
{
    DEVICE,
    DEVICE_UPLOAD,
    HOST_UPLOAD,
    HOST_READBACK,
    MAX_NUM,
}
