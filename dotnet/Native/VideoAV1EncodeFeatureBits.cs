namespace SharpNRI.Native;

[NativeTypeName("uint32_t")]
[Flags]
public enum VideoAV1EncodeFeatureBits : uint
{
    NONE = 0,
    ORDER_HINT_TOOLS = (1U << (0)),
    LOOP_RESTORATION_FILTER = (1U << (1)),
    FORCED_INTEGER_MOTION_VECTORS = (1U << (2)),
    AUTO_SEGMENTATION = (1U << (3)),
    CDEF_FILTERING = (1U << (4)),
    QUANTIZATION_DELTAS = (1U << (5)),
    LOOP_FILTER_DELTAS = (1U << (6)),
}
