namespace SharpNRI.Native;

public unsafe partial struct VideoEncodeDesc
{
    [NativeTypeName("nri::VideoSession *")]
    public VideoSession* session;

    [NativeTypeName("nri::VideoSessionParameters *")]
    public VideoSessionParameters* parameters;

    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* srcPicture;

    [NativeTypeName("nri::VideoBitstreamRange")]
    public VideoBitstreamRange dstBitstream;

    /// <summary>bytes of codec metadata already written before the current frame payload</summary>
    [NativeTypeName("uint64_t")]
    public ulong bitstreamMetadataSize;

    [NativeTypeName("const VideoEncodePictureDesc *")]
    public VideoEncodePictureDesc* pictureDesc;

    [NativeTypeName("const VideoEncodeRateControlDesc *")]
    public VideoEncodeRateControlDesc* rateControlDesc;

    [NativeTypeName("nri::VideoEncodeBits")]
    public VideoEncodeBits flags;

    /// <summary>reconstructed/DPB setup picture; required for sessions with DPB slots, also for non-reference pictures (B frames, AV1 "refreshFrameFlags = 0"): the slot is written, but not referenced afterwards</summary>
    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* reconstructedPicture;

    /// <summary>opaque encode metadata; required if "VideoCapabilities::metadataSize" is non-zero</summary>
    [NativeTypeName("nri::Buffer *")]
    public Buffer* metadata;

    [NativeTypeName("uint64_t")]
    public ulong metadataOffset;

    /// <summary>opaque feedback metadata consumed by "GetVideoEncodeFeedback" after the required resolve operation; a pending range must not be reused</summary>
    [NativeTypeName("nri::Buffer *")]
    public Buffer* resolvedMetadata;

    [NativeTypeName("uint64_t")]
    public ulong resolvedMetadataOffset;

    [NativeTypeName("const VideoReference *")]
    public VideoReference* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;

    [NativeTypeName("uint32_t")]
    public uint reconstructedSlot;

    [NativeTypeName("const VideoH264EncodePictureDesc *")]
    public VideoH264EncodePictureDesc* h264PictureDesc;

    /// <summary>parameter set selection; all ids default to 0</summary>
    [NativeTypeName("const VideoH265EncodePictureDesc *")]
    public VideoH265EncodePictureDesc* h265PictureDesc;

    [NativeTypeName("const VideoAV1EncodePictureDesc *")]
    public VideoAV1EncodePictureDesc* av1PictureDesc;

    [NativeTypeName("const VideoH265ReferenceDesc *")]
    public VideoH265ReferenceDesc* h265ReferenceDescs;
}
