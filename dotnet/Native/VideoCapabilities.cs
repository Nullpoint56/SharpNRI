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

    /// <summary>maximum "bitstream.size" for decode and "dstBitstream.size" for encode</summary>
    [NativeTypeName("uint64_t")]
    public ulong bitstreamSizeMax;

    [NativeTypeName("uint32_t")]
    public uint metadataOffsetAlignment;

    [NativeTypeName("uint32_t")]
    public uint resolvedMetadataOffsetAlignment;

    /// <summary>maximum per-session feedback ranges not yet consumed by "GetVideoEncodeFeedback"; reset by "ResetVideoSession"</summary>
    [NativeTypeName("uint32_t")]
    public uint encodeFeedbackMaxPendingNum;

    /// <summary>minimum "VideoEncodeDesc::metadata" range size in bytes</summary>
    [NativeTypeName("uint64_t")]
    public ulong metadataSize;

    /// <summary>minimum "VideoEncodeDesc::resolvedMetadata" range size in bytes</summary>
    [NativeTypeName("uint64_t")]
    public ulong resolvedMetadataSize;

    [NativeTypeName("nri::VideoDecodeBitstreamSourceBits")]
    public VideoDecodeBitstreamSourceBits decodeBitstreamSourceMask;

    /// <summary>required state while the resolved metadata is written</summary>
    [NativeTypeName("nri::AccessStage")]
    public AccessStage resolvedMetadataState;

    /// <summary>queue used by the operation that writes resolved metadata</summary>
    [NativeTypeName("nri::QueueType")]
    public QueueType resolvedMetadataQueueType;

    /// <summary>minimum layer count when "dpbTextureArrayRequired" is true</summary>
    [NativeTypeName("uint32_t")]
    public uint dpbTextureArrayMinLayerNum;

    /// <summary>decode output and reconstructed/DPB setup may use the same texture subresource</summary>
    [NativeTypeName("bool")]
    public byte decodeDpbAndOutputCoincide;

    /// <summary>decode output and reconstructed/DPB setup may use different texture subresources (textures with the session extent)</summary>
    [NativeTypeName("bool")]
    public byte decodeDpbAndOutputDistinct;

    /// <summary>decode DPB pictures (setup and references) must use "VIDEO_REFERENCE_ONLY" textures, which only video decode can access</summary>
    [NativeTypeName("bool")]
    public byte decodeReferenceOnlyRequired;

    /// <summary>all decode or encode DPB pictures must use layers of one texture</summary>
    [NativeTypeName("bool")]
    public byte dpbTextureArrayRequired;

    [NativeTypeName("bool")]
    public byte decodeNativeArgumentsSupported;

    /// <summary>if false, encode output ranges must extend to the end of the buffer</summary>
    [NativeTypeName("bool")]
    public byte encodeBitstreamRangeSizeSupported;

    [NativeTypeName("bool")]
    public byte encodeFeedbackSupported;

    /// <summary>if true, call "CmdResolveVideoEncodeFeedback" after the encode</summary>
    [NativeTypeName("bool")]
    public byte encodeFeedbackResolveRequired;

    /// <summary>H.264/H.265: if false, active L0 (P/B) and L1 (B) counts must match PPS defaults (1 per list for default H.265 parameters)</summary>
    [NativeTypeName("bool")]
    public byte encodeReferenceCountOverrideSupported;
}
