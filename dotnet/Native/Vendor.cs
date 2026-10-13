namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum Vendor : byte
{
    UNKNOWN,
    NVIDIA,
    AMD,
    INTEL,
    MAX_NUM,
}
