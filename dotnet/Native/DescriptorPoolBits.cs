namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum DescriptorPoolBits : byte
{
    NONE = 0,
    /// <summary>required by sets containing "DescriptorRangeBits::ALLOW_UPDATE_AFTER_SET" ranges</summary>
    ALLOW_UPDATE_AFTER_SET = (1 << 0),
    /// <summary>allows allocated descriptor sets to be used as sources in "CopyDescriptorRanges"; such sets can't be bound</summary>
    COPY_SOURCE = (1 << 1),
}
