using System;
namespace SharpNRI.Native;

[NativeTypeName("uint16_t")]
[Flags]
public enum UpscalerBits : ushort
{
    NONE = 0,
    HDR = (1 << 0),
    SRGB = (1 << 1),
    USE_EXPOSURE = (1 << 2),
    USE_REACTIVE = (1 << 3),
    DEPTH_INVERTED = (1 << 4),
    DEPTH_INFINITE = (1 << 5),
    DEPTH_LINEAR = (1 << 6),
    MV_UPSCALED = (1 << 7),
    MV_JITTERED = (1 << 8),
}
