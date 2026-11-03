using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum MicromapBits : byte
{
    NONE = 0,
    ALLOW_COMPACTION = (1 << 1),
    PREFER_FAST_TRACE = (1 << 2),
    PREFER_FAST_BUILD = (1 << 3),
}
