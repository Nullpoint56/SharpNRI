using System;
namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum RayTracingPipelineBits : byte
{
    NONE = 0,
    SKIP_TRIANGLES = (1 << 0),
    SKIP_AABBS = (1 << 1),
    ALLOW_MICROMAPS = (1 << 2),
    FAIL_ON_CACHE_MISS = (1 << 3),
}
