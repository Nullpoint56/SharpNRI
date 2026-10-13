using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum AccelerationStructureBits : byte
{
    NONE = 0,
    ALLOW_UPDATE = (1 << 0),
    ALLOW_COMPACTION = (1 << 1),
    ALLOW_DATA_ACCESS = (1 << 2),
    ALLOW_MICROMAP_UPDATE = (1 << 3),
    ALLOW_DISABLE_MICROMAPS = (1 << 4),
    PREFER_FAST_TRACE = (1 << 5),
    PREFER_FAST_BUILD = (1 << 6),
    MINIMIZE_MEMORY = (1 << 7),
}
