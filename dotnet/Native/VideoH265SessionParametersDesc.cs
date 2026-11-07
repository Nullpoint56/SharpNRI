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

    /// <summary>defaults to "videoParameterSetNum"</summary>
    [NativeTypeName("uint32_t")]
    public uint maxVideoParameterSetNum;

    /// <summary>defaults to "sequenceParameterSetNum"</summary>
    [NativeTypeName("uint32_t")]
    public uint maxSequenceParameterSetNum;

    /// <summary>defaults to "pictureParameterSetNum"</summary>
    [NativeTypeName("uint32_t")]
    public uint maxPictureParameterSetNum;
}
