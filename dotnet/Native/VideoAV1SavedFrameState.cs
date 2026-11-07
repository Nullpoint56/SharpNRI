namespace SharpNRI.Native;

/// <summary>State of a reference frame that later frame headers can depend on ("RefUpscaledWidth", "load_previous", "load_grain_params")</summary>
public partial struct VideoAV1SavedFrameState
{
    /// <summary>"refDeltas" and "modeDeltas"</summary>
    [NativeTypeName("nri::VideoAV1LoopFilterDesc")]
    public VideoAV1LoopFilterDesc loopFilter;

    /// <summary>"FeatureEnabled" and "FeatureData"</summary>
    [NativeTypeName("nri::VideoAV1SegmentationDesc")]
    public VideoAV1SegmentationDesc segmentation;

    /// <summary>"gm_params"</summary>
    [NativeTypeName("nri::VideoAV1GlobalMotionDesc")]
    public VideoAV1GlobalMotionDesc globalMotion;

    /// <summary>all zero if no grain is applied</summary>
    [NativeTypeName("nri::VideoAV1FilmGrainDesc")]
    public VideoAV1FilmGrainDesc filmGrain;

    [NativeTypeName("uint16_t")]
    public ushort upscaledWidthMinus1;

    [NativeTypeName("uint16_t")]
    public ushort frameHeightMinus1;

    [NativeTypeName("uint16_t")]
    public ushort renderWidthMinus1;

    [NativeTypeName("uint16_t")]
    public ushort renderHeightMinus1;

    [NativeTypeName("uint8_t")]
    public byte orderHint;

    /// <summary>"apply_grain" ("load_grain_params" restores it)</summary>
    [NativeTypeName("uint8_t")]
    public byte applyGrain;

    /// <summary>0 if the frame inherited state that was not provided</summary>
    [NativeTypeName("uint8_t")]
    public byte valid;
}
