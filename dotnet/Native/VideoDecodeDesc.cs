namespace SharpNRI.Native;

public unsafe partial struct VideoDecodeDesc
{
    [NativeTypeName("nri::VideoSession *")]
    public VideoSession* session;

    [NativeTypeName("nri::VideoSessionParameters *")]
    public VideoSessionParameters* parameters;

    /// <summary>provide at least one source supported by "VideoCapabilities::decodeBitstreamSourceMask"</summary>
    [NativeTypeName("nri::VideoDecodeBitstreamRange")]
    public VideoDecodeBitstreamRange bitstream;

    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* dstPicture;

    /// <summary>reconstructed/DPB setup picture; required for distinct mode, may alias "dstPicture" in coincide mode. "APPLY_GRAIN" requires a distinct setup picture in sessions with DPB slots</summary>
    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* setupPicture;

    [NativeTypeName("const VideoReference *")]
    public VideoReference* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;

    /// <summary>default setup slot, always written; neutral H.264 may override it with "referenceSlot"; native DXVA "CurrPic" must use "dstSlot" and reference "PicEntry" indices must match "VideoReference::slot"</summary>
    [NativeTypeName("uint32_t")]
    public uint dstSlot;

    /// <summary>for non-reference pictures, use a slot whose picture is no longer needed, never one referenced by this or a later decode native DXVA frame arguments; if omitted, the neutral codec picture description is used</summary>
    [NativeTypeName("const VideoDecodeArgument *")]
    public VideoDecodeArgument* arguments;

    [NativeTypeName("uint32_t")]
    public uint argumentNum;

    /// <summary>neutral H.264 picture description; required if "argumentNum" is 0</summary>
    [NativeTypeName("const VideoH264DecodePictureDesc *")]
    public VideoH264DecodePictureDesc* h264PictureDesc;

    /// <summary>neutral H.265 picture description; required if "argumentNum" is 0</summary>
    [NativeTypeName("const VideoH265DecodePictureDesc *")]
    public VideoH265DecodePictureDesc* h265PictureDesc;

    /// <summary>neutral AV1 picture description; required if "argumentNum" is 0</summary>
    [NativeTypeName("const VideoAV1DecodePictureDesc *")]
    public VideoAV1DecodePictureDesc* av1PictureDesc;
}
