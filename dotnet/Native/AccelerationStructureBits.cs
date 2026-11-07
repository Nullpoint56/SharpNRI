namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum AccelerationStructureBits : byte
{
    NONE = 0,
    /// <summary>allows to do "updates", which are faster than "builds" (may increase memory usage, build time and decrease traversal performance)</summary>
    ALLOW_UPDATE = (1 << 0),
    /// <summary>allows to compact the acceleration structure by copying using "COMPACT" mode</summary>
    ALLOW_COMPACTION = (1 << 1),
    /// <summary>allows to access vertex data from shaders (requires "features.rayTracingPositionFetch")</summary>
    ALLOW_DATA_ACCESS = (1 << 2),
    /// <summary>allows to update micromaps via acceleration structure update (may increase size and decrease traversal performance)</summary>
    ALLOW_MICROMAP_UPDATE = (1 << 3),
    /// <summary>allows to have "DISABLE_MICROMAPS" flag for instances referencing this BLAS</summary>
    ALLOW_DISABLE_MICROMAPS = (1 << 4),
    /// <summary>prioritize traversal performance over build time</summary>
    PREFER_FAST_TRACE = (1 << 5),
    /// <summary>prioritize build time over traversal performance</summary>
    PREFER_FAST_BUILD = (1 << 6),
    /// <summary>minimize the amount of memory used during the build (may increase build time and decrease traversal performance)</summary>
    MINIMIZE_MEMORY = (1 << 7),
}
