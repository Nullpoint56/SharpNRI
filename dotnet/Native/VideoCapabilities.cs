namespace SharpNRI.Native;

public partial struct VideoCapabilities
{
    [NativeTypeName("uint32_t")]
    public uint widthMin;

    [NativeTypeName("uint32_t")]
    public uint heightMin;

    [NativeTypeName("uint32_t")]
    public uint widthMax;

    [NativeTypeName("uint32_t")]
    public uint heightMax;

    [NativeTypeName("uint32_t")]
    public uint pictureAccessGranularityWidth;

    [NativeTypeName("uint32_t")]
    public uint pictureAccessGranularityHeight;

    [NativeTypeName("uint32_t")]
    public uint maxReferenceNum;

    [NativeTypeName("uint32_t")]
    public uint bitstreamOffsetAlignment;

    [NativeTypeName("uint32_t")]
    public uint bitstreamSizeAlignment;

    [NativeTypeName("uint64_t")]
    public ulong bitstreamSizeMax;

    [NativeTypeName("uint32_t")]
    public uint metadataOffsetAlignment;

    [NativeTypeName("uint32_t")]
    public uint resolvedMetadataOffsetAlignment;

    [NativeTypeName("uint32_t")]
    public uint encodeFeedbackMaxPendingNum;

    [NativeTypeName("uint64_t")]
    public ulong metadataSize;

    [NativeTypeName("uint64_t")]
    public ulong resolvedMetadataSize;

    [NativeTypeName("nri::VideoDecodeBitstreamSourceBits")]
    public VideoDecodeBitstreamSourceBits decodeBitstreamSourceMask;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage resolvedMetadataState;

    [NativeTypeName("nri::QueueType")]
    public QueueType resolvedMetadataQueueType;

    [NativeTypeName("uint32_t")]
    public uint dpbTextureArrayMinLayerNum;

    [NativeTypeName("bool")]
    public byte decodeDpbAndOutputCoincide;

    [NativeTypeName("bool")]
    public byte decodeDpbAndOutputDistinct;

    [NativeTypeName("bool")]
    public byte decodeReferenceOnlyRequired;

    [NativeTypeName("bool")]
    public byte dpbTextureArrayRequired;

    [NativeTypeName("bool")]
    public byte decodeNativeArgumentsSupported;

    [NativeTypeName("bool")]
    public byte encodeBitstreamRangeSizeSupported;

    [NativeTypeName("bool")]
    public byte encodeFeedbackSupported;

    [NativeTypeName("bool")]
    public byte encodeFeedbackResolveRequired;

    [NativeTypeName("bool")]
    public byte encodeReferenceCountOverrideSupported;
}
