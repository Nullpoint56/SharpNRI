namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum AddressMode : byte
{
    REPEAT,
    MIRRORED_REPEAT,
    CLAMP_TO_EDGE,
    CLAMP_TO_BORDER,
    MIRROR_CLAMP_TO_EDGE,
    MAX_NUM,
}
