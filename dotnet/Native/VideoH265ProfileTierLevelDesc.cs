namespace SharpNRI.Native;

public partial struct VideoH265ProfileTierLevelDesc
{
    [NativeTypeName("nri::VideoH265ProfileTierLevelBits")]
    public VideoH265ProfileTierLevelBits flags;

    [NativeTypeName("uint8_t")]
    public byte generalProfileIdc;

    /// <summary>"general_level_idc" as coded in the bitstream: 30 times the level number (e.g. 120 = level 4)</summary>
    [NativeTypeName("uint8_t")]
    public byte generalLevelIdc;
}
