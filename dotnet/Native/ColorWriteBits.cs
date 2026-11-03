using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum ColorWriteBits : byte
{
    NONE = 0,
    R = (1 << 0),
    G = (1 << 1),
    B = (1 << 2),
    A = (1 << 3),
    RGB = R | G | B,
    RGBA = RGB | A,
}
