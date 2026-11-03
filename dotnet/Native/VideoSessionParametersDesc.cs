namespace SharpNRI.Native;

public unsafe partial struct VideoSessionParametersDesc
{
    [NativeTypeName("nri::VideoSession *")]
    public VideoSession* session;

    [NativeTypeName("const VideoH264SessionParametersDesc *")]
    public VideoH264SessionParametersDesc* h264Parameters;

    [NativeTypeName("const VideoH265SessionParametersDesc *")]
    public VideoH265SessionParametersDesc* h265Parameters;

    [NativeTypeName("const VideoAV1SessionParametersDesc *")]
    public VideoAV1SessionParametersDesc* av1Parameters;
}
