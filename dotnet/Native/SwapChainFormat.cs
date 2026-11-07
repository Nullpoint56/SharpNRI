namespace SharpNRI.Native;

/// <summary>Color space: - BT.709 - LDR https://en.wikipedia.org/wiki/Rec._709 - BT.2020 - HDR https://en.wikipedia.org/wiki/Rec._2020 - https://registry.khronos.org/DataFormat/specs/1.4/dataformat.1.4.html#PRIMARY_CONVERSION Transfer function: - G10 - linear (gamma 1.0) - G22 - sRGB (gamma ~2.2) - G2084 - SMPTE ST.2084 (Perceptual Quantization) - https://registry.khronos.org/DataFormat/specs/1.4/dataformat.1.4.html#TRANSFER_CONVERSION Bits per channel: - 8, 10, 16 (float)</summary>
[NativeTypeName("uint8_t")]
public enum SwapChainFormat : byte
{
    BT709_G10_16BIT,
    BT709_G22_8BIT,
    BT709_G22_10BIT,
    BT2020_G2084_10BIT,
    MAX_NUM,
}
