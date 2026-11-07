namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum PlaneBits : byte
{
    ALL = 0,
    NONE = (1 << 7),
    COLOR = (1 << 0),
    DEPTH = (1 << 1),
    STENCIL = (1 << 2),
    PLANE_0 = (1 << 3),
    PLANE_1 = (1 << 4),
    PLANE_2 = (1 << 5),
    PLANE_Y = PLANE_0,
    PLANE_UV = PLANE_1,
}
