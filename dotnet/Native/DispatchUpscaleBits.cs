using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum DispatchUpscaleBits : byte
{
    NONE = 0,
    RESET_HISTORY = (1 << 0),
    USE_SPECULAR_MOTION = (1 << 1),
}
