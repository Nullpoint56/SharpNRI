namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoPictureUsage : byte
{
    DECODE_OUTPUT,
    DECODE_REFERENCE,
    ENCODE_INPUT,
    ENCODE_REFERENCE,
    MAX_NUM,
}
