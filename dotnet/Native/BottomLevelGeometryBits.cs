namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum BottomLevelGeometryBits : byte
{
    NONE = 0,
    /// <summary>the geometry acts as if no any hit shader is present (can be overriden by "TopLevelInstanceBits" or ray flags)</summary>
    OPAQUE_GEOMETRY = (1 << 0),
    /// <summary>the any-hit shader must be called once for each primitive in this geometry</summary>
    NO_DUPLICATE_ANY_HIT_INVOCATION = (1 << 1),
}
