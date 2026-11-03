namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoCodec : byte
{
    NONE,
    H264,
    H265,
    AV1,
    MAX_NUM,
}
