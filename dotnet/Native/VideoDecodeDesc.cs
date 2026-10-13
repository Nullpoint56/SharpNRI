namespace PrismNRI.Native;

public unsafe partial struct VideoDecodeDesc
{
    [NativeTypeName("nri::VideoSession *")]
    public VideoSession* session;

    [NativeTypeName("nri::VideoSessionParameters *")]
    public VideoSessionParameters* parameters;

    [NativeTypeName("nri::VideoDecodeBitstreamRange")]
    public VideoDecodeBitstreamRange bitstream;

    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* dstPicture;

    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* setupPicture;

    [NativeTypeName("const VideoReference *")]
    public VideoReference* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;

    [NativeTypeName("uint32_t")]
    public uint dstSlot;

    [NativeTypeName("const VideoDecodeArgument *")]
    public VideoDecodeArgument* arguments;

    [NativeTypeName("uint32_t")]
    public uint argumentNum;

    [NativeTypeName("const VideoH264DecodePictureDesc *")]
    public VideoH264DecodePictureDesc* h264PictureDesc;

    [NativeTypeName("const VideoH265DecodePictureDesc *")]
    public VideoH265DecodePictureDesc* h265PictureDesc;

    [NativeTypeName("const VideoAV1DecodePictureDesc *")]
    public VideoAV1DecodePictureDesc* av1PictureDesc;
}
