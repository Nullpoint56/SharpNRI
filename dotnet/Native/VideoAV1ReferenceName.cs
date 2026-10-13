namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoAV1ReferenceName : byte
{
    NONE,
    LAST,
    LAST2,
    LAST3,
    GOLDEN,
    BWDREF,
    ALTREF2,
    ALTREF,
    MAX_NUM,
}
