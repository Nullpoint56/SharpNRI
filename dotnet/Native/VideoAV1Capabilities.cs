namespace PrismNRI.Native;

public partial struct VideoAV1Capabilities
{
    [NativeTypeName("uint32_t")]
    public uint av1MaxLevel;

    [NativeTypeName("uint32_t")]
    public uint av1MaxTileColumnNum;

    [NativeTypeName("uint32_t")]
    public uint av1MaxTileRowNum;

    [NativeTypeName("uint32_t")]
    public uint av1MinTileWidth;

    [NativeTypeName("uint32_t")]
    public uint av1MinTileHeight;

    [NativeTypeName("uint32_t")]
    public uint av1MaxTileWidth;

    [NativeTypeName("uint32_t")]
    public uint av1MaxTileHeight;

    [NativeTypeName("uint32_t")]
    public uint av1SuperblockSizeMask;

    [NativeTypeName("uint32_t")]
    public uint av1MaxSingleReferenceNum;

    [NativeTypeName("uint32_t")]
    public uint av1SingleReferenceNameMask;

    [NativeTypeName("uint32_t")]
    public uint av1MaxUnidirectionalCompoundReferenceNum;

    [NativeTypeName("uint32_t")]
    public uint av1UnidirectionalCompoundReferenceNameMask;

    [NativeTypeName("uint32_t")]
    public uint av1MaxBidirectionalCompoundReferenceNum;

    [NativeTypeName("uint32_t")]
    public uint av1BidirectionalCompoundReferenceNameMask;

    [NativeTypeName("uint32_t")]
    public uint av1MaxTemporalLayerNum;

    [NativeTypeName("uint32_t")]
    public uint av1MaxSpatialLayerNum;

    [NativeTypeName("uint32_t")]
    public uint av1MaxOperatingPointNum;

    [NativeTypeName("uint32_t")]
    public uint av1MinQIndex;

    [NativeTypeName("uint32_t")]
    public uint av1MaxQIndex;

    [NativeTypeName("nri::VideoAV1EncodeFeatureBits")]
    public VideoAV1EncodeFeatureBits av1EncodeRequiredFeatureFlags;

    [NativeTypeName("nri::VideoAV1EncodeFeatureBits")]
    public VideoAV1EncodeFeatureBits av1EncodeSupportedFeatureFlags;
}
