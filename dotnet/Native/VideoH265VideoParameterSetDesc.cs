namespace SharpNRI.Native;

public partial struct VideoH265VideoParameterSetDesc
{
    [NativeTypeName("nri::VideoH265VideoParameterSetBits")]
    public VideoH265VideoParameterSetBits flags;

    [NativeTypeName("uint8_t")]
    public byte videoParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte maxSubLayersMinus1;

    [NativeTypeName("uint32_t")]
    public uint numUnitsInTick;

    [NativeTypeName("uint32_t")]
    public uint timeScale;

    [NativeTypeName("uint32_t")]
    public uint numTicksPocDiffOneMinus1;

    [NativeTypeName("nri::VideoH265ProfileTierLevelDesc")]
    public VideoH265ProfileTierLevelDesc profileTierLevel;

    [NativeTypeName("nri::VideoH265DecPicBufMgrDesc")]
    public VideoH265DecPicBufMgrDesc decPicBufMgr;
}
