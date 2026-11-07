namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoH265DecodePictureBits : byte
{
    NONE = 0,
    IRAP = (1 << 0),
    IDR = (1 << 1),
    REFERENCE = (1 << 2),
    SHORT_TERM_REF_PIC_SET_SPS = (1 << 3),
}
