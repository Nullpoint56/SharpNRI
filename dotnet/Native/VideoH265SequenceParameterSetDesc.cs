namespace SharpNRI.Native;

public unsafe partial struct VideoH265SequenceParameterSetDesc
{
    [NativeTypeName("nri::VideoH265SequenceParameterSetBits")]
    public VideoH265SequenceParameterSetBits flags;

    [NativeTypeName("uint8_t")]
    public byte videoParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte maxSubLayersMinus1;

    [NativeTypeName("uint8_t")]
    public byte sequenceParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte chromaFormatIdc;

    [NativeTypeName("uint32_t")]
    public uint pictureWidthInLumaSamples;

    [NativeTypeName("uint32_t")]
    public uint pictureHeightInLumaSamples;

    [NativeTypeName("uint8_t")]
    public byte bitDepthLumaMinus8;

    [NativeTypeName("uint8_t")]
    public byte bitDepthChromaMinus8;

    [NativeTypeName("uint8_t")]
    public byte log2MaxPictureOrderCountLsbMinus4;

    [NativeTypeName("uint8_t")]
    public byte log2MinLumaCodingBlockSizeMinus3;

    [NativeTypeName("uint8_t")]
    public byte log2DiffMaxMinLumaCodingBlockSize;

    [NativeTypeName("uint8_t")]
    public byte log2MinLumaTransformBlockSizeMinus2;

    [NativeTypeName("uint8_t")]
    public byte log2DiffMaxMinLumaTransformBlockSize;

    [NativeTypeName("uint8_t")]
    public byte maxTransformHierarchyDepthInter;

    [NativeTypeName("uint8_t")]
    public byte maxTransformHierarchyDepthIntra;

    [NativeTypeName("uint8_t")]
    public byte pcmSampleBitDepthLumaMinus1;

    [NativeTypeName("uint8_t")]
    public byte pcmSampleBitDepthChromaMinus1;

    [NativeTypeName("uint8_t")]
    public byte log2MinPcmLumaCodingBlockSizeMinus3;

    [NativeTypeName("uint8_t")]
    public byte log2DiffMaxMinPcmLumaCodingBlockSize;

    [NativeTypeName("uint32_t")]
    public uint confWinLeftOffset;

    [NativeTypeName("uint32_t")]
    public uint confWinRightOffset;

    [NativeTypeName("uint32_t")]
    public uint confWinTopOffset;

    [NativeTypeName("uint32_t")]
    public uint confWinBottomOffset;

    [NativeTypeName("const VideoH265ShortTermRefPicSetDesc *")]
    public VideoH265ShortTermRefPicSetDesc* shortTermRefPicSets;

    [NativeTypeName("uint8_t")]
    public byte numShortTermRefPicSets;

    [NativeTypeName("uint8_t")]
    public byte numLongTermRefPicsSps;

    [NativeTypeName("nri::VideoH265ProfileTierLevelDesc")]
    public VideoH265ProfileTierLevelDesc profileTierLevel;

    [NativeTypeName("nri::VideoH265DecPicBufMgrDesc")]
    public VideoH265DecPicBufMgrDesc decPicBufMgr;

    [NativeTypeName("const VideoH265ScalingListsDesc *")]
    public VideoH265ScalingListsDesc* scalingLists;

    [NativeTypeName("const VideoH265LongTermRefPicsSpsDesc *")]
    public VideoH265LongTermRefPicsSpsDesc* longTermRefPicsSps;
}
