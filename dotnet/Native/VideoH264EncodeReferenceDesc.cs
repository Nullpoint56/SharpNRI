namespace PrismNRI.Native;

public partial struct VideoH264EncodeReferenceDesc
{
    [NativeTypeName("nri::VideoFrameType")]
    public VideoFrameType frameType;

    [NativeTypeName("uint8_t")]
    public byte temporalLayer;

    [NativeTypeName("uint8_t")]
    public byte listIndex;

    [NativeTypeName("uint8_t")]
    public byte longTermReference;

    [NativeTypeName("uint32_t")]
    public uint frameNum;

    [NativeTypeName("int32_t")]
    public int pictureOrderCount;

    [NativeTypeName("uint32_t")]
    public uint slot;

    [NativeTypeName("uint16_t")]
    public ushort longTermPictureIndex;

    [NativeTypeName("uint16_t")]
    public ushort longTermFrameIndex;
}
