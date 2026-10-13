using System;
namespace PrismNRI.Native;

[NativeTypeName("uint16_t")]
[Flags]
public enum VideoH264SequenceParameterSetBits : ushort
{
    NONE = 0,
    CONSTRAINT_SET0 = (1 << 0),
    CONSTRAINT_SET1 = (1 << 1),
    CONSTRAINT_SET2 = (1 << 2),
    CONSTRAINT_SET3 = (1 << 3),
    CONSTRAINT_SET4 = (1 << 4),
    CONSTRAINT_SET5 = (1 << 5),
    DIRECT_8X8_INFERENCE = (1 << 6),
    MB_ADAPTIVE_FRAME_FIELD = (1 << 7),
    FRAME_MBS_ONLY = (1 << 8),
    DELTA_PIC_ORDER_ALWAYS_ZERO = (1 << 9),
    SEPARATE_COLOUR_PLANE = (1 << 10),
    GAPS_IN_FRAME_NUM_ALLOWED = (1 << 11),
    QPPRIME_Y_ZERO_TRANSFORM_BYPASS = (1 << 12),
}
