namespace SharpNRI.Native;

public unsafe partial struct VideoAV1EncodePictureDesc
{
    [NativeTypeName("uint32_t")]
    public uint currentFrameId;

    [NativeTypeName("uint8_t")]
    public byte orderHint;

    [NativeTypeName("uint8_t")]
    public byte refreshFrameFlags;

    [NativeTypeName("nri::VideoAV1ReferenceName")]
    public VideoAV1ReferenceName primaryReferenceName;

    [NativeTypeName("nri::VideoAV1PictureBits")]
    public VideoAV1PictureBits flags;

    [NativeTypeName("uint16_t")]
    public ushort renderWidthMinus1;

    [NativeTypeName("uint16_t")]
    public ushort renderHeightMinus1;

    [NativeTypeName("uint8_t")]
    public byte codedDenom;

    /// <summary>AV1 "interpolation_filter": 0..4 (4 = SWITCHABLE)</summary>
    [NativeTypeName("uint8_t")]
    public byte interpolationFilter;

    /// <summary>0 selects the default, else AV1 "TxMode" 1..2 (LARGEST, SELECT); ONLY_4X4 is implied for lossless frames</summary>
    [NativeTypeName("uint8_t")]
    public byte txMode;

    [NativeTypeName("uint8_t")]
    public byte baseQIndex;

    [NativeTypeName("uint8_t")]
    public byte cdefDampingMinus3;

    [NativeTypeName("uint8_t")]
    public byte cdefBits;

    [NativeTypeName("uint8_t")]
    public byte deltaQRes;

    [NativeTypeName("uint8_t")]
    public byte deltaLfRes;

    [NativeTypeName("const VideoAV1TileLayoutDesc *")]
    public VideoAV1TileLayoutDesc* tileLayout;

    [NativeTypeName("const VideoAV1QuantizationDesc *")]
    public VideoAV1QuantizationDesc* quantization;

    [NativeTypeName("const VideoAV1LoopFilterDesc *")]
    public VideoAV1LoopFilterDesc* loopFilter;

    [NativeTypeName("const VideoAV1CdefDesc *")]
    public VideoAV1CdefDesc* cdef;

    [NativeTypeName("const VideoAV1SegmentationDesc *")]
    public VideoAV1SegmentationDesc* segmentation;

    [NativeTypeName("const VideoAV1LoopRestorationDesc *")]
    public VideoAV1LoopRestorationDesc* loopRestoration;

    [NativeTypeName("const VideoAV1GlobalMotionDesc *")]
    public VideoAV1GlobalMotionDesc* globalMotion;

    /// <summary>if provided, must include 8 entries</summary>
    [NativeTypeName("const uint8_t *")]
    public byte* orderHints;

    [NativeTypeName("const VideoAV1ReferenceDesc *")]
    public VideoAV1ReferenceDesc* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;
}
