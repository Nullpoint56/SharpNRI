namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum VideoH265ShortTermRefPicSetBits : byte
{
    NONE = 0,
    INTER_REF_PIC_SET_PREDICTION = (1 << 0),
    DELTA_RPS_SIGN = (1 << 1),
}
