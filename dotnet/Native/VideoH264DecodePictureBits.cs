using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoH264DecodePictureBits : byte
{
    NONE = 0,
    FIELD_PICTURE = (1 << 0),
    INTRA = (1 << 1),
    IDR = (1 << 2),
    BOTTOM_FIELD = (1 << 3),
    REFERENCE = (1 << 4),
    COMPLEMENTARY_FIELD_PAIR = (1 << 5),
}
