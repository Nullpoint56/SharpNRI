using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoAV1EncodeDecodeInfo
{
    /// <summary>offset relative to VideoEncodeFeedback::encodedBitstreamOffset</summary>
    [NativeTypeName("uint64_t")]
    public ulong bitstreamOffset;

    [NativeTypeName("uint64_t")]
    public ulong bitstreamSize;

    [NativeTypeName("nri::VideoAV1SequenceDesc")]
    public VideoAV1SequenceDesc sequence;

    [NativeTypeName("nri::VideoAV1DecodePictureDesc")]
    public VideoAV1DecodePictureDesc picture;

    [NativeTypeName("VideoAV1DecodeTileDesc[64]")]
    public _tiles_e__FixedBuffer tiles;

    [NativeTypeName("nri::VideoAV1TileLayoutDesc")]
    public VideoAV1TileLayoutDesc tileLayout;

    [NativeTypeName("uint16_t[65]")]
    public _miColumnStarts_e__FixedBuffer miColumnStarts;

    [NativeTypeName("uint16_t[65]")]
    public _miRowStarts_e__FixedBuffer miRowStarts;

    [NativeTypeName("uint16_t[64]")]
    public _widthInSuperblocksMinus1_e__FixedBuffer widthInSuperblocksMinus1;

    [NativeTypeName("uint16_t[64]")]
    public _heightInSuperblocksMinus1_e__FixedBuffer heightInSuperblocksMinus1;

    /// <summary>7 named entries, then a NONE entry for every other valid reference frame map entry</summary>
    [NativeTypeName("VideoAV1ReferenceDesc[15]")]
    public _references_e__FixedBuffer references;

    [NativeTypeName("nri::VideoAV1QuantizationDesc")]
    public VideoAV1QuantizationDesc quantization;

    [NativeTypeName("nri::VideoAV1LoopFilterDesc")]
    public VideoAV1LoopFilterDesc loopFilter;

    [NativeTypeName("nri::VideoAV1CdefDesc")]
    public VideoAV1CdefDesc cdef;

    [NativeTypeName("nri::VideoAV1SegmentationDesc")]
    public VideoAV1SegmentationDesc segmentation;

    [NativeTypeName("nri::VideoAV1LoopRestorationDesc")]
    public VideoAV1LoopRestorationDesc loopRestoration;

    [NativeTypeName("nri::VideoAV1GlobalMotionDesc")]
    public VideoAV1GlobalMotionDesc globalMotion;

    /// <summary>resolved parameters ("load_grain_params" applied)</summary>
    [NativeTypeName("nri::VideoAV1FilmGrainDesc")]
    public VideoAV1FilmGrainDesc filmGrain;

    /// <summary>store into "savedFrameStates[i]" for every slot "i" in "picture.refreshFrameFlags"</summary>
    [NativeTypeName("nri::VideoAV1SavedFrameState")]
    public VideoAV1SavedFrameState savedFrameState;

    [InlineArray(64)]
    public partial struct _tiles_e__FixedBuffer
    {
        public VideoAV1DecodeTileDesc e0;
    }

    [InlineArray(65)]
    public partial struct _miColumnStarts_e__FixedBuffer
    {
        public ushort e0;
    }

    [InlineArray(65)]
    public partial struct _miRowStarts_e__FixedBuffer
    {
        public ushort e0;
    }

    [InlineArray(64)]
    public partial struct _widthInSuperblocksMinus1_e__FixedBuffer
    {
        public ushort e0;
    }

    [InlineArray(64)]
    public partial struct _heightInSuperblocksMinus1_e__FixedBuffer
    {
        public ushort e0;
    }

    [InlineArray(15)]
    public partial struct _references_e__FixedBuffer
    {
        public VideoAV1ReferenceDesc e0;
    }
}
