namespace SharpNRI.Native;

/// <summary>When discarding recorded video commands, reset affected command buffers and call "ResetVideoSession" before recording further commands for the session Threadsafe: no</summary>
public unsafe partial struct VideoInterface
{
    /// <summary>Session {</summary>
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

    /// <summary>Returns the states required for caller-recorded decode/encode picture barriers</summary>
    [NativeTypeName("Result (*)(const VideoPicture &, VideoPictureRole, VideoPictureState &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoPicture*, VideoPictureRole, VideoPictureState*, Result> GetVideoPictureState;

    /// <summary>Serializes H.264 SPS/PPS or H.265 VPS/SPS/PPS parameter sets to Annex-B bytes. Pass "dst = nullptr" to query the required byte size in "writtenSize"</summary>
    [NativeTypeName("Result (*)(VideoAnnexBParameterSetsDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoAnnexBParameterSetsDesc*, Result> WriteVideoAnnexBParameterSets;

    /// <summary>Serializes H.264 end-of-sequence/end-of-stream or H.265 EOS/EOB Annex-B NAL units. Pass "dst = nullptr" to query the required byte size in "writtenSize"</summary>
    [NativeTypeName("Result (*)(VideoAnnexBEndOfStreamDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoAnnexBEndOfStreamDesc*, Result> WriteVideoAnnexBEndOfStream;

    /// <summary>Serializes AV1 temporal delimiter and sequence header OBUs. Pass "dst = nullptr" to query the required byte size in "writtenSize"</summary>
    [NativeTypeName("Result (*)(VideoAV1ObuHeadersDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoAV1ObuHeadersDesc*, Result> WriteVideoAV1ObuHeaders;

    /// <summary>Command buffer { Video decode/encode command buffers must be created from "QueueType::VIDEO_DECODE" or "QueueType::VIDEO_ENCODE" queues. Commands for the same video session must be recorded in submission order</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const VideoDecodeDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, VideoDecodeDesc*, void> CmdDecodeVideo;

    [NativeTypeName("void (*)(CommandBuffer &, const VideoEncodeDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, VideoEncodeDesc*, void> CmdEncodeVideo;

    /// <summary>Required only if "VideoCapabilities::encodeFeedbackResolveRequired" is true. Uses the advertised queue and buffer state</summary>
    [NativeTypeName("void (*)(CommandBuffer &, VideoSession &, Buffer &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, VideoSession*, Buffer*, ulong, void> CmdResolveVideoEncodeFeedback;

    [NativeTypeName("Result (*)(VideoSession &, Buffer &, uint64_t, VideoEncodeFeedback &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoSession*, Buffer*, ulong, VideoEncodeFeedback*, Result> GetVideoEncodeFeedback;

    [NativeTypeName("Result (*)(VideoSession &, Buffer &, uint64_t, const VideoAV1EncodeDecodeInfoDesc &, VideoAV1EncodeDecodeInfo &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<VideoSession*, Buffer*, ulong, VideoAV1EncodeDecodeInfoDesc*, VideoAV1EncodeDecodeInfo*, Result> GetVideoAV1EncodeDecodeInfo;
}
