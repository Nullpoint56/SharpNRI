namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum ComponentSwizzle : byte
{
    IDENTITY,
    ZERO,
    ONE,
    R,
    G,
    B,
    A,
    MAX_NUM,
}
