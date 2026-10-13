namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoPictureRole : byte
{
    DECODE_OUTPUT,
    DECODE_REFERENCE,
    DECODE_SETUP,
    DECODE_OUTPUT_AND_SETUP,
    ENCODE_INPUT,
    ENCODE_REFERENCE,
    ENCODE_RECONSTRUCTED,
    MAX_NUM,
}
