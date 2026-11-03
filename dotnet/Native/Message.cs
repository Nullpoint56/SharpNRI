namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum Message : byte
{
    INFO,
    WARNING,
    ERROR,
    MAX_NUM,
}
