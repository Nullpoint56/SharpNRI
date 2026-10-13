using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoDecodeBitstreamSourceBits : byte
{
    NONE = 0,
    BUFFER = (1 << 0),
    HOST = (1 << 1),
}
