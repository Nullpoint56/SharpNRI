namespace SharpNRI.Native;

public partial struct VideoAV1SavedFrameState
{
    [NativeTypeName("nri::VideoAV1LoopFilterDesc")]
    public VideoAV1LoopFilterDesc loopFilter;

    [NativeTypeName("nri::VideoAV1SegmentationDesc")]
    public VideoAV1SegmentationDesc segmentation;

    [NativeTypeName("nri::VideoAV1GlobalMotionDesc")]
    public VideoAV1GlobalMotionDesc globalMotion;

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

    [NativeTypeName("uint8_t")]
    public byte applyGrain;

    [NativeTypeName("uint8_t")]
    public byte valid;
}
