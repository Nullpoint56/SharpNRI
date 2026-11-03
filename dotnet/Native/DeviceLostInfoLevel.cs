namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum DeviceLostInfoLevel : byte
{
    NONE,
    BASIC,
    VERBOSE,
    MAX_NUM,
}
