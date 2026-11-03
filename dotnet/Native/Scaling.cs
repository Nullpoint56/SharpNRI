namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum Scaling : byte
{
    ONE_TO_ONE,
    STRETCH,
    MAX_NUM,
}
