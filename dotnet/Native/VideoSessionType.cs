namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoSessionType : byte
{
    DECODE,
    ENCODE,
    MAX_NUM,
}
