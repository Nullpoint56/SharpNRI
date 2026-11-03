namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum ResolveOp : byte
{
    AVERAGE,
    MIN,
    MAX,
    SAMPLE_ZERO,
    MAX_NUM,
}
