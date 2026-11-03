using System;
namespace SharpNRI.Native;

[NativeTypeName("uint32_t")]
[Flags]
public enum VideoAV1PictureBits : uint
{
    NONE = 0,
    ERROR_RESILIENT_MODE = (1U << (0)),
    DISABLE_CDF_UPDATE = (1U << (1)),
    USE_SUPERRES = (1U << (2)),
    RENDER_AND_FRAME_SIZE_DIFFERENT = (1U << (3)),
    ALLOW_SCREEN_CONTENT_TOOLS = (1U << (4)),
    IS_FILTER_SWITCHABLE = (1U << (5)),
    FORCE_INTEGER_MV = (1U << (6)),
    FRAME_SIZE_OVERRIDE = (1U << (7)),
    BUFFER_REMOVAL_TIME_PRESENT = (1U << (8)),
    ALLOW_INTRABC = (1U << (9)),
    FRAME_REFS_SHORT_SIGNALING = (1U << (10)),
    ALLOW_HIGH_PRECISION_MV = (1U << (11)),
    IS_MOTION_MODE_SWITCHABLE = (1U << (12)),
    USE_REF_FRAME_MVS = (1U << (13)),
    DISABLE_FRAME_END_UPDATE_CDF = (1U << (14)),
    ALLOW_WARPED_MOTION = (1U << (15)),
    REDUCED_TX_SET = (1U << (16)),
    REFERENCE_SELECT = (1U << (17)),
    SKIP_MODE_PRESENT = (1U << (18)),
    DELTA_Q_PRESENT = (1U << (19)),
    DELTA_LF_PRESENT = (1U << (20)),
    DELTA_LF_MULTI = (1U << (21)),
    SEGMENTATION_ENABLED = (1U << (22)),
    SEGMENTATION_UPDATE_MAP = (1U << (23)),
    SEGMENTATION_TEMPORAL_UPDATE = (1U << (24)),
    SEGMENTATION_UPDATE_DATA = (1U << (25)),
    USES_LR = (1U << (26)),
    USES_CHROMA_LR = (1U << (27)),
    SHOW_FRAME = (1U << (28)),
    SHOWABLE_FRAME = (1U << (29)),
    APPLY_GRAIN = (1U << (30)),
}
