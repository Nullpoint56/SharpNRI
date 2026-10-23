namespace PrismNRI.Native;

public partial struct VideoH264SequenceParameterSetDesc
{
    [NativeTypeName("nri::VideoH264SequenceParameterSetBits")]
    public VideoH264SequenceParameterSetBits flags;

    [NativeTypeName("uint8_t")]
    public byte profileIdc;

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

    [NativeTypeName("uint16_t")]
    public ushort frameCropLeftOffset;

    [NativeTypeName("uint16_t")]
    public ushort frameCropRightOffset;

    [NativeTypeName("uint16_t")]
    public ushort frameCropTopOffset;

    [NativeTypeName("uint16_t")]
    public ushort frameCropBottomOffset;
}
