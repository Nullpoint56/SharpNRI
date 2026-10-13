using System.Runtime.CompilerServices;

namespace PrismNRI.Native;

public unsafe partial struct VideoH265PictureParameterSetDesc
{
    [NativeTypeName("nri::VideoH265PictureParameterSetBits")]
    public VideoH265PictureParameterSetBits flags;

    [NativeTypeName("uint8_t")]
    public byte pictureParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte sequenceParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte videoParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte numExtraSliceHeaderBits;

    [NativeTypeName("uint8_t")]
    public byte refIndexL0DefaultActiveMinus1;

    [NativeTypeName("uint8_t")]
    public byte refIndexL1DefaultActiveMinus1;

    [NativeTypeName("int8_t")]
    public sbyte initQpMinus26;

    [NativeTypeName("uint8_t")]
    public byte diffCuQpDeltaDepth;

    [NativeTypeName("int8_t")]
    public sbyte cbQpOffset;

    [NativeTypeName("int8_t")]
    public sbyte crQpOffset;

    [NativeTypeName("int8_t")]
    public sbyte betaOffsetDiv2;

    [NativeTypeName("int8_t")]
    public sbyte tcOffsetDiv2;

    [NativeTypeName("uint8_t")]
    public byte log2ParallelMergeLevelMinus2;

    [NativeTypeName("uint8_t")]
    public byte tileColumnNumMinus1;

    [NativeTypeName("uint8_t")]
    public byte tileRowNumMinus1;

    [NativeTypeName("uint16_t[19]")]
    public _columnWidthMinus1_e__FixedBuffer columnWidthMinus1;

    [NativeTypeName("uint16_t[21]")]
    public _rowHeightMinus1_e__FixedBuffer rowHeightMinus1;

    [NativeTypeName("const VideoH265ScalingListsDesc *")]
    public VideoH265ScalingListsDesc* scalingLists;

    [InlineArray(19)]
    public partial struct _columnWidthMinus1_e__FixedBuffer
    {
        public ushort e0;
    }

    [InlineArray(21)]
    public partial struct _rowHeightMinus1_e__FixedBuffer
    {
        public ushort e0;
    }
}
