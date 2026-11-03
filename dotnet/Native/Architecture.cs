namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum Architecture : byte
{
    UNKNOWN,
    SOFTWARE,
    VIRTUAL,
    INTEGRATED,
    DISCRETE,
    MAX_NUM,
}
