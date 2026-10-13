using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoEncodeBits : byte
{
    NONE = 0,
    FORCE_KEY_FRAME = (1 << 0),
}
