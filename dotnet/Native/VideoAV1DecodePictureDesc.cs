using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public unsafe partial struct VideoAV1DecodePictureDesc
{
    [NativeTypeName("nri::VideoFrameType")]
    public VideoFrameType frameType;

    [NativeTypeName("uint8_t")]
    public byte orderHint;

    [NativeTypeName("uint8_t")]
    public byte refreshFrameFlags;

    [NativeTypeName("nri::VideoAV1ReferenceName")]
    public VideoAV1ReferenceName primaryReferenceName;

    [NativeTypeName("uint32_t")]
    public uint currentFrameId;

    [NativeTypeName("uint32_t")]
    public uint frameHeaderOffset;

    [NativeTypeName("nri::VideoAV1PictureBits")]
    public VideoAV1PictureBits flags;

    [NativeTypeName("uint16_t")]
    public ushort renderWidthMinus1;

    [NativeTypeName("uint16_t")]
    public ushort renderHeightMinus1;

    [NativeTypeName("uint8_t")]
    public byte baseQIndex;

    [NativeTypeName("uint8_t")]
    public byte superresDenom;

    [NativeTypeName("uint8_t")]
    public byte codedDenom;

    /// <summary>AV1 "interpolation_filter": 0..4 (4 = SWITCHABLE)</summary>
    [NativeTypeName("uint8_t")]
    public byte interpolationFilter;

    /// <summary>AV1 "TxMode": 0..2 (ONLY_4X4, LARGEST, SELECT)</summary>
    [NativeTypeName("uint8_t")]
    public byte txMode;

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

    /// <summary>used with "APPLY_GRAIN": resolved parameters ("load_grain_params" applied, e.g. "VideoAV1EncodeDecodeInfo::filmGrain")</summary>
    [NativeTypeName("const VideoAV1FilmGrainDesc *")]
    public VideoAV1FilmGrainDesc* filmGrain;

    /// <summary>8 entries indexed like AV1 "OrderHints[]" by reference frame name: 0 = INTRA_FRAME, 1..7 = LAST..ALTREF; named reference entries are overridden by "references[].orderHint"</summary>
    [NativeTypeName("const uint8_t *")]
    public byte* orderHints;

    /// <summary>"SkipModeFrame[0..1]" used with "SKIP_MODE_PRESENT"</summary>
    [NativeTypeName("VideoAV1ReferenceName[2]")]
    public _skipModeFrames_e__FixedBuffer skipModeFrames;

    [NativeTypeName("const VideoAV1DecodeTileDesc *")]
    public VideoAV1DecodeTileDesc* tiles;

    [NativeTypeName("uint32_t")]
    public uint tileNum;

    [NativeTypeName("const VideoAV1ReferenceDesc *")]
    public VideoAV1ReferenceDesc* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;

    [InlineArray(2)]
    public partial struct _skipModeFrames_e__FixedBuffer
    {
        public VideoAV1ReferenceName e0;
    }
}
