namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum MicromapBits : byte
{
    NONE = 0,
    /// <summary>allows to compact the micromap by copying using "COMPACT" mode</summary>
    ALLOW_COMPACTION = (1 << 1),
    /// <summary>prioritize traversal performance over build time</summary>
    PREFER_FAST_TRACE = (1 << 2),
    /// <summary>prioritize build time over traversal performance</summary>
    PREFER_FAST_BUILD = (1 << 3),
}
