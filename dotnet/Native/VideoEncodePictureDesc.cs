namespace SharpNRI.Native;

public partial struct VideoEncodePictureDesc
{
    [NativeTypeName("nri::VideoFrameType")]
    public VideoFrameType frameType;

    [NativeTypeName("uint8_t")]
    public byte temporalLayer;

    [NativeTypeName("uint16_t")]
    public ushort idrPictureId;

    [NativeTypeName("uint32_t")]
    public uint frameIndex;

    [NativeTypeName("int32_t")]
    public int pictureOrderCount;
}
