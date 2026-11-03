namespace SharpNRI.Native;

public partial struct VideoH265ReferenceDesc
{
    [NativeTypeName("uint32_t")]
    public uint slot;

    [NativeTypeName("int32_t")]
    public int pictureOrderCount;

    [NativeTypeName("uint8_t")]
    public byte temporalLayer;

    [NativeTypeName("nri::VideoFrameType")]
    public VideoFrameType frameType;

    [NativeTypeName("uint8_t")]
    public byte longTerm;

    [NativeTypeName("uint8_t")]
    public byte listIndex;
}
