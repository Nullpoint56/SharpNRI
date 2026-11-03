namespace SharpNRI.Native;

public unsafe partial struct VideoH265SessionParametersDesc
{
    [NativeTypeName("const VideoH265VideoParameterSetDesc *")]
    public VideoH265VideoParameterSetDesc* videoParameterSets;

    [NativeTypeName("uint32_t")]
    public uint videoParameterSetNum;

    [NativeTypeName("const VideoH265SequenceParameterSetDesc *")]
    public VideoH265SequenceParameterSetDesc* sequenceParameterSets;

    [NativeTypeName("uint32_t")]
    public uint sequenceParameterSetNum;

    [NativeTypeName("const VideoH265PictureParameterSetDesc *")]
    public VideoH265PictureParameterSetDesc* pictureParameterSets;

    [NativeTypeName("uint32_t")]
    public uint pictureParameterSetNum;

    [NativeTypeName("uint32_t")]
    public uint maxVideoParameterSetNum;

    [NativeTypeName("uint32_t")]
    public uint maxSequenceParameterSetNum;

    [NativeTypeName("uint32_t")]
    public uint maxPictureParameterSetNum;
}
