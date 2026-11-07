namespace SharpNRI.Native;

[NativeTypeName("uint32_t")]
[Flags]
public enum TopLevelInstanceBits : uint
{
    NONE = 0,
    /// <summary>disables face culling for this instance</summary>
    TRIANGLE_CULL_DISABLE = (1U << (0)),
    /// <summary>inverts the facing determination for geometry in this instance (since the facing is determined in object space, an instance transform does not change the winding, but a geometry transform does)</summary>
    TRIANGLE_FLIP_FACING = (1U << (1)),
    /// <summary>force enable "OPAQUE_GEOMETRY" bit on all geometries referenced by this instance</summary>
    FORCE_OPAQUE = (1U << (2)),
    /// <summary>force disable "OPAQUE_GEOMETRY" bit on all geometries referenced by this instance</summary>
    FORCE_NON_OPAQUE = (1U << (3)),
    /// <summary>ignore the "unknown" state and only consider the "transparent" or "opaque" bit for all 4-state micromaps encountered during traversal</summary>
    FORCE_OPACITY_2_STATE = (1U << (4)),
    /// <summary>disable micromap test for all triangles and revert to using geometry opaque/non-opaque state instead</summary>
    DISABLE_MICROMAPS = (1U << (5)),
}
