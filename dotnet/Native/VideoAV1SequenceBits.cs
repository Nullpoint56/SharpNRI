using System;
namespace PrismNRI.Native;

[NativeTypeName("uint32_t")]
[Flags]
public enum VideoAV1SequenceBits : uint
{
    NONE = 0,
    STILL_PICTURE = (1U << (0)),
    REDUCED_STILL_PICTURE_HEADER = (1U << (1)),
    USE_128X128_SUPERBLOCK = (1U << (2)),
    ENABLE_FILTER_INTRA = (1U << (3)),
    ENABLE_INTRA_EDGE_FILTER = (1U << (4)),
    ENABLE_INTERINTRA_COMPOUND = (1U << (5)),
    ENABLE_MASKED_COMPOUND = (1U << (6)),
    ENABLE_WARPED_MOTION = (1U << (7)),
    ENABLE_DUAL_FILTER = (1U << (8)),
    ENABLE_ORDER_HINT = (1U << (9)),
    ENABLE_JNT_COMP = (1U << (10)),
    ENABLE_REF_FRAME_MVS = (1U << (11)),
    FRAME_ID_NUMBERS_PRESENT = (1U << (12)),
    ENABLE_SUPERRES = (1U << (13)),
    ENABLE_CDEF = (1U << (14)),
    ENABLE_RESTORATION = (1U << (15)),
    FILM_GRAIN_PARAMS_PRESENT = (1U << (16)),
    TIMING_INFO_PRESENT = (1U << (17)),
    INITIAL_DISPLAY_DELAY_PRESENT = (1U << (18)),
    MONO_CHROME = (1U << (19)),
    COLOR_RANGE = (1U << (20)),
    SEPARATE_UV_DELTA_Q = (1U << (21)),
    COLOR_DESCRIPTION_PRESENT = (1U << (22)),
}
