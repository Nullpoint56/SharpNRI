namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoEncodeRateControlMode : byte
{
    CQP,
    CBR,
    VBR,
    MAX_NUM,
}
