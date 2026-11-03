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

    [NativeTypeName("uint64_t")]
    public ulong bitstreamMetadataSize;

    [NativeTypeName("const VideoEncodePictureDesc *")]
    public VideoEncodePictureDesc* pictureDesc;

    [NativeTypeName("const VideoEncodeRateControlDesc *")]
    public VideoEncodeRateControlDesc* rateControlDesc;

    [NativeTypeName("nri::VideoEncodeBits")]
    public VideoEncodeBits flags;

    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* reconstructedPicture;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* metadata;

    [NativeTypeName("uint64_t")]
    public ulong metadataOffset;

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

    [NativeTypeName("const VideoH265EncodePictureDesc *")]
    public VideoH265EncodePictureDesc* h265PictureDesc;

    [NativeTypeName("const VideoAV1EncodePictureDesc *")]
    public VideoAV1EncodePictureDesc* av1PictureDesc;

    [NativeTypeName("const VideoH265ReferenceDesc *")]
    public VideoH265ReferenceDesc* h265ReferenceDescs;
}
