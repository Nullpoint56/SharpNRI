namespace PrismNRI.Native;

public unsafe partial struct VideoH265DecodePictureDesc
{
    [NativeTypeName("nri::VideoH265DecodePictureBits")]
    public VideoH265DecodePictureBits flags;

    [NativeTypeName("uint8_t")]
    public byte videoParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte sequenceParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte pictureParameterSetId;

    [NativeTypeName("int32_t")]
    public int pictureOrderCount;

    [NativeTypeName("uint8_t")]
    public byte numDeltaPocsOfRefRpsIdx;

    [NativeTypeName("uint16_t")]
    public ushort numBitsForShortTermRefPicSetInSlice;

    [NativeTypeName("const uint32_t *")]
    public uint* sliceSegmentOffsets;

    [NativeTypeName("uint32_t")]
    public uint sliceSegmentOffsetNum;

    [NativeTypeName("const VideoH265ReferenceDesc *")]
    public VideoH265ReferenceDesc* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;
}
