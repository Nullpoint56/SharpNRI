namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkColorComponentFlagBits.html</summary>
[NativeTypeName("uint8_t")]
[Flags]
public enum ColorWriteBits : byte
{
    NONE = 0,
    R = (1 << 0),
    G = (1 << 1),
    B = (1 << 2),
    A = (1 << 3),
    /// <summary>"wingdi.h" must not be included after</summary>
    RGB = R | G | B,
    RGBA = RGB | A,
}
