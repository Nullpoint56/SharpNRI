namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum BindPoint : byte
{
    INHERIT,
    GRAPHICS,
    COMPUTE,
    RAY_TRACING,
    MAX_NUM,
}
