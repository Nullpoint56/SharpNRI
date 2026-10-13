using System;
namespace PrismNRI.Native;

[NativeTypeName("uint16_t")]
[Flags]
public enum TextureUsageBits : ushort
{
    NONE = 0,
    SHADER_RESOURCE = (1 << 0),
    SHADER_RESOURCE_STORAGE = (1 << 1),
    COLOR_ATTACHMENT = (1 << 2),
    DEPTH_STENCIL_ATTACHMENT = (1 << 3),
    SHADING_RATE_ATTACHMENT = (1 << 4),
    INPUT_ATTACHMENT = (1 << 5),
    HOST_TRANSFER = (1 << 6),
    VIDEO_DECODE = (1 << 7),
    VIDEO_ENCODE = (1 << 8),
    VIDEO_REFERENCE_ONLY = (1 << 9),
}
