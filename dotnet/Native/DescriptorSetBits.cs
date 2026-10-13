using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum DescriptorSetBits : byte
{
    NONE = 0,
    ALLOW_UPDATE_AFTER_SET = (1 << 0),
}
