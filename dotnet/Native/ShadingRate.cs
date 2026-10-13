namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum ShadingRate : byte
{
    FRAGMENT_SIZE_1X1,
    FRAGMENT_SIZE_1X2,
    FRAGMENT_SIZE_2X1,
    FRAGMENT_SIZE_2X2,
    FRAGMENT_SIZE_2X4,
    FRAGMENT_SIZE_4X2,
    FRAGMENT_SIZE_4X4,
    MAX_NUM,
}
