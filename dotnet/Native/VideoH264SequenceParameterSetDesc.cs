namespace SharpNRI.Native;

public partial struct VideoH264SequenceParameterSetDesc
{
    [NativeTypeName("nri::VideoH264SequenceParameterSetBits")]
    public VideoH264SequenceParameterSetBits flags;

    [NativeTypeName("uint8_t")]
    public byte profileIdc;

    /// <summary>"level_idc" as coded in the bitstream: 10 times the level number (e.g. 41 = level 4.1), level 1b is not supported</summary>
    [NativeTypeName("uint8_t")]
    public byte levelIdc;

    [NativeTypeName("uint8_t")]
    public byte chromaFormatIdc;

    [NativeTypeName("uint8_t")]
    public byte sequenceParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte bitDepthLumaMinus8;

    [NativeTypeName("uint8_t")]
    public byte bitDepthChromaMinus8;

    [NativeTypeName("uint8_t")]
    public byte log2MaxFrameNumMinus4;

    [NativeTypeName("uint8_t")]
    public byte pictureOrderCountType;

    [NativeTypeName("int32_t")]
    public int offsetForNonReferencePicture;

    [NativeTypeName("int32_t")]
    public int offsetForTopToBottomField;

    [NativeTypeName("uint8_t")]
    public byte log2MaxPictureOrderCountLsbMinus4;

    [NativeTypeName("uint8_t")]
    public byte referenceFrameNum;

    [NativeTypeName("uint16_t")]
    public ushort pictureWidthInMbsMinus1;

    [NativeTypeName("uint16_t")]
    public ushort pictureHeightInMapUnitsMinus1;

    /// <summary>used with "FRAME_CROPPING", in "CropUnitX" units (2 luma samples for 4:2:0)</summary>
    [NativeTypeName("uint16_t")]
    public ushort frameCropLeftOffset;

    [NativeTypeName("uint16_t")]
    public ushort frameCropRightOffset;

    /// <summary>used with "FRAME_CROPPING", in "CropUnitY" units (2 luma samples for 4:2:0 progressive)</summary>
    [NativeTypeName("uint16_t")]
    public ushort frameCropTopOffset;

    [NativeTypeName("uint16_t")]
    public ushort frameCropBottomOffset;
}
