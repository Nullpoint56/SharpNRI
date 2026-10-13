namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoFrameType : byte
{
    IDR,
    I,
    P,
    B,
    MAX_NUM,
}
