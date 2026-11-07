namespace SharpNRI.Native;

[NativeTypeName("uint32_t")]
[Flags]
public enum VideoH265SequenceParameterSetBits : uint
{
    NONE = 0,
    TEMPORAL_ID_NESTING = (1U << (0)),
    SEPARATE_COLOUR_PLANE = (1U << (1)),
    CONFORMANCE_WINDOW = (1U << (2)),
    SUB_LAYER_ORDERING_INFO_PRESENT = (1U << (3)),
    SCALING_LIST_ENABLED = (1U << (4)),
    SCALING_LIST_DATA_PRESENT = (1U << (5)),
    AMP_ENABLED = (1U << (6)),
    SAMPLE_ADAPTIVE_OFFSET_ENABLED = (1U << (7)),
    PCM_ENABLED = (1U << (8)),
    PCM_LOOP_FILTER_DISABLED = (1U << (9)),
    LONG_TERM_REF_PICS_PRESENT = (1U << (10)),
    TEMPORAL_MVP_ENABLED = (1U << (11)),
    STRONG_INTRA_SMOOTHING_ENABLED = (1U << (12)),
    VUI_PARAMETERS_PRESENT = (1U << (13)),
}
