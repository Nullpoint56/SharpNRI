namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum Message : byte
{
    INFO,
    WARNING,
    /// <summary>"wingdi.h" must not be included after</summary>
    ERROR,
    MAX_NUM,
}
