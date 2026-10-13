namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum UpscalerMode : byte
{
    NATIVE,
    ULTRA_QUALITY,
    QUALITY,
    BALANCED,
    PERFORMANCE,
    ULTRA_PERFORMANCE,
    MAX_NUM,
}
