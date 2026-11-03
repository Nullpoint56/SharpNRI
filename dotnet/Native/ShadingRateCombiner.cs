namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum ShadingRateCombiner : byte
{
    KEEP,
    REPLACE,
    MIN,
    MAX,
    SUM,
    MAX_NUM,
}
