using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoH264DecodeReferenceBits : byte
{
    NONE = 0,
    TOP_FIELD = (1 << 0),
    BOTTOM_FIELD = (1 << 1),
    LONG_TERM = (1 << 2),
    NON_EXISTING = (1 << 3),
}
