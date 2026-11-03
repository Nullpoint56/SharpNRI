using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoH264PictureParameterSetBits : byte
{
    NONE = 0,
    TRANSFORM_8X8_MODE = (1 << 0),
    REDUNDANT_PIC_CNT_PRESENT = (1 << 1),
    CONSTRAINED_INTRA_PRED = (1 << 2),
    DEBLOCKING_FILTER_CONTROL_PRESENT = (1 << 3),
    WEIGHTED_PRED = (1 << 4),
    BOTTOM_FIELD_PIC_ORDER_IN_FRAME = (1 << 5),
    ENTROPY_CODING_MODE = (1 << 6),
}
