using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum GraphicsPipelineBits : byte
{
    NONE = 0,
    FAIL_ON_CACHE_MISS = (1 << 0),
}
