using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum DescriptorRangeBits : byte
{
    NONE = 0,
    PARTIALLY_BOUND = (1 << 0),
    ARRAY = (1 << 1),
    VARIABLE_SIZED_ARRAY = (1 << 2),
    ALLOW_UPDATE_AFTER_SET = (1 << 3),
}
