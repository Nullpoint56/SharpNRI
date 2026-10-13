namespace PrismNRI.Native;

public unsafe partial struct VideoH264SessionParametersDesc
{
    [NativeTypeName("const VideoH264SequenceParameterSetDesc *")]
    public VideoH264SequenceParameterSetDesc* sequenceParameterSets;

    [NativeTypeName("uint32_t")]
    public uint sequenceParameterSetNum;

    [NativeTypeName("const VideoH264PictureParameterSetDesc *")]
    public VideoH264PictureParameterSetDesc* pictureParameterSets;

    [NativeTypeName("uint32_t")]
    public uint pictureParameterSetNum;

    [NativeTypeName("uint32_t")]
    public uint maxSequenceParameterSetNum;

    [NativeTypeName("uint32_t")]
    public uint maxPictureParameterSetNum;
}
