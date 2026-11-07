namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoH265ProfileTierLevelBits : byte
{
    NONE = 0,
    TIER = (1 << 0),
    PROGRESSIVE_SOURCE = (1 << 1),
    INTERLACED_SOURCE = (1 << 2),
    NON_PACKED_CONSTRAINT = (1 << 3),
    FRAME_ONLY_CONSTRAINT = (1 << 4),
}
