using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoH265VideoParameterSetBits : byte
{
    NONE = 0,
    TEMPORAL_ID_NESTING = (1 << 0),
    SUB_LAYER_ORDERING_INFO_PRESENT = (1 << 1),
    TIMING_INFO_PRESENT = (1 << 2),
    POC_PROPORTIONAL_TO_TIMING = (1 << 3),
}
