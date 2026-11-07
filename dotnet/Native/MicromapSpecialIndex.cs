namespace SharpNRI.Native;

[NativeTypeName("int8_t")]
public enum MicromapSpecialIndex : sbyte
{
    /// <summary>2/4 state specifies that the entire triangle is fully transparent</summary>
    FULLY_TRANSPARENT = -1,
    /// <summary>specifies that the entire triangle is fully opaque</summary>
    FULLY_OPAQUE = -2,
    /// <summary>4 state specifies that the entire triangle is unknown-transparent</summary>
    FULLY_UNKNOWN_TRANSPARENT = -3,
    /// <summary>specifies that the entire triangle is unknown-opaque</summary>
    FULLY_UNKNOWN_OPAQUE = -4,
    MAX_NUM,
}
