namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum SwapChainFormat : byte
{
    BT709_G10_16BIT,
    BT709_G22_8BIT,
    BT709_G22_10BIT,
    BT2020_G2084_10BIT,
    MAX_NUM,
}
