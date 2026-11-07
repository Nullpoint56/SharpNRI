namespace SharpNRI.Native;

public unsafe partial struct VideoAnnexBParameterSetsDesc
{
    [NativeTypeName("nri::VideoCodec")]
    public VideoCodec codec;

    [NativeTypeName("const VideoH264SequenceParameterSetDesc *")]
    public VideoH264SequenceParameterSetDesc* h264Sps;

    [NativeTypeName("const VideoH264PictureParameterSetDesc *")]
    public VideoH264PictureParameterSetDesc* h264Pps;

    [NativeTypeName("const VideoH265VideoParameterSetDesc *")]
    public VideoH265VideoParameterSetDesc* h265Vps;

    [NativeTypeName("const VideoH265SequenceParameterSetDesc *")]
    public VideoH265SequenceParameterSetDesc* h265Sps;

    [NativeTypeName("const VideoH265PictureParameterSetDesc *")]
    public VideoH265PictureParameterSetDesc* h265Pps;

    /// <summary>if null, only "writtenSize" is returned</summary>
    [NativeTypeName("uint8_t *")]
    public byte* dst;

    [NativeTypeName("uint64_t")]
    public ulong dstSize;

    [NativeTypeName("uint64_t")]
    public ulong writtenSize;
}
