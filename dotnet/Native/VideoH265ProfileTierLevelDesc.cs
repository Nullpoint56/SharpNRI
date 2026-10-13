namespace PrismNRI.Native;

public partial struct VideoH265ProfileTierLevelDesc
{
    [NativeTypeName("nri::VideoH265ProfileTierLevelBits")]
    public VideoH265ProfileTierLevelBits flags;

    [NativeTypeName("uint8_t")]
    public byte generalProfileIdc;

    [NativeTypeName("uint8_t")]
    public byte generalLevelIdc;
}
