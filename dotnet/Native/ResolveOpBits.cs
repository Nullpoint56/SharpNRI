using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum ResolveOpBits : byte
{
    NONE = 0,
    AVERAGE = (1 << 0),
    MIN = (1 << 1),
    MAX = (1 << 2),
    SAMPLE_ZERO = (1 << 3),
}
