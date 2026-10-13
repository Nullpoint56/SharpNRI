namespace PrismNRI.Native;

public unsafe partial struct VideoInterface
{
    [NativeTypeName("Result (*)(const Device &, const VideoSessionDesc &, VideoCapabilities &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, VideoSessionDesc*, VideoCapabilities*, Result> GetVideoCapabilities;

    [NativeTypeName("Result (*)(const Device &, const VideoSessionDesc &, VideoAV1Capabilities &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, VideoSessionDesc*, VideoAV1Capabilities*, Result> GetVideoAV1Capabilities;

    [NativeTypeName("Result (*)(Device &, const VideoSessionDesc &, VideoSession *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, VideoSessionDesc*, VideoSession**, Result> CreateVideoSession;

    [NativeTypeName("Result (*)(Device &, const VideoSessionParametersDesc &, VideoSessionParameters *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, VideoSessionParametersDesc*, VideoSessionParameters**, Result> CreateVideoSessionParameters;

    [NativeTypeName("Result (*)(Device &, const VideoPictureDesc &, VideoPicture *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, VideoPictureDesc*, VideoPicture**, Result> CreateVideoPicture;

    [NativeTypeName("void (*)(VideoSession *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoSession*, void> DestroyVideoSession;

    [NativeTypeName("void (*)(VideoSession &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoSession*, void> ResetVideoSession;

    [NativeTypeName("void (*)(VideoSessionParameters *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoSessionParameters*, void> DestroyVideoSessionParameters;

    [NativeTypeName("void (*)(VideoPicture *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoPicture*, void> DestroyVideoPicture;

    [NativeTypeName("Result (*)(const VideoPicture &, VideoPictureRole, VideoPictureState &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoPicture*, VideoPictureRole, VideoPictureState*, Result> GetVideoPictureState;

    [NativeTypeName("Result (*)(VideoAnnexBParameterSetsDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoAnnexBParameterSetsDesc*, Result> WriteVideoAnnexBParameterSets;

    [NativeTypeName("Result (*)(VideoAnnexBEndOfStreamDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoAnnexBEndOfStreamDesc*, Result> WriteVideoAnnexBEndOfStream;

    [NativeTypeName("Result (*)(VideoAV1ObuHeadersDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoAV1ObuHeadersDesc*, Result> WriteVideoAV1ObuHeaders;

    [NativeTypeName("void (*)(CommandBuffer &, const VideoDecodeDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, VideoDecodeDesc*, void> CmdDecodeVideo;

    [NativeTypeName("void (*)(CommandBuffer &, const VideoEncodeDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, VideoEncodeDesc*, void> CmdEncodeVideo;

    [NativeTypeName("void (*)(CommandBuffer &, VideoSession &, Buffer &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, VideoSession*, Buffer*, ulong, void> CmdResolveVideoEncodeFeedback;

    [NativeTypeName("Result (*)(VideoSession &, Buffer &, uint64_t, VideoEncodeFeedback &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoSession*, Buffer*, ulong, VideoEncodeFeedback*, Result> GetVideoEncodeFeedback;

    [NativeTypeName("Result (*)(VideoSession &, Buffer &, uint64_t, const VideoAV1EncodeDecodeInfoDesc &, VideoAV1EncodeDecodeInfo &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoSession*, Buffer*, ulong, VideoAV1EncodeDecodeInfoDesc*, VideoAV1EncodeDecodeInfo*, Result> GetVideoAV1EncodeDecodeInfo;
}
