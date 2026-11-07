namespace SharpNRI.Native;

public unsafe partial struct VideoSessionParametersDesc
{
    [NativeTypeName("nri::VideoSession *")]
    public VideoSession* session;

    /// <summary>required for H.264 except decode using native arguments on a supporting backend</summary>
    [NativeTypeName("const VideoH264SessionParametersDesc *")]
    public VideoH264SessionParametersDesc* h264Parameters;

    /// <summary>required for neutral H.265 decode; optional for native decode arguments and encode</summary>
    [NativeTypeName("const VideoH265SessionParametersDesc *")]
    public VideoH265SessionParametersDesc* h265Parameters;

    [NativeTypeName("const VideoAV1SessionParametersDesc *")]
    public VideoAV1SessionParametersDesc* av1Parameters;
}
