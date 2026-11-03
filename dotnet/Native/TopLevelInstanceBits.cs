using System;
namespace SharpNRI.Native;

[NativeTypeName("uint32_t")]
[Flags]
public enum TopLevelInstanceBits : uint
{
    NONE = 0,
    TRIANGLE_CULL_DISABLE = (1U << (0)),
    TRIANGLE_FLIP_FACING = (1U << (1)),
    FORCE_OPAQUE = (1U << (2)),
    FORCE_NON_OPAQUE = (1U << (3)),
    FORCE_OPACITY_2_STATE = (1U << (4)),
    DISABLE_MICROMAPS = (1U << (5)),
}
