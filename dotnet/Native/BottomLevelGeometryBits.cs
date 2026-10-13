using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum BottomLevelGeometryBits : byte
{
    NONE = 0,
    OPAQUE_GEOMETRY = (1 << 0),
    NO_DUPLICATE_ANY_HIT_INVOCATION = (1 << 1),
}
