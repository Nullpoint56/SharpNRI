namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum SharingMode : byte
{
    CONCURRENT,
    EXCLUSIVE,
    SIMULTANEOUS,
    MAX_NUM,
}
