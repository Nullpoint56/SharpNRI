namespace SharpNRI.Native;

public unsafe partial struct VideoH264DecodePictureDesc
{
    [NativeTypeName("nri::VideoH264DecodePictureBits")]
    public VideoH264DecodePictureBits flags;

    [NativeTypeName("uint8_t")]
    public byte sequenceParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte pictureParameterSetId;

    [NativeTypeName("uint16_t")]
    public ushort frameNum;

    [NativeTypeName("uint16_t")]
    public ushort idrPictureId;

    [NativeTypeName("int32_t")]
    public int topFieldOrderCount;

    [NativeTypeName("int32_t")]
    public int bottomFieldOrderCount;

    [NativeTypeName("const uint32_t *")]
    public uint* sliceOffsets;

    [NativeTypeName("uint32_t")]
    public uint sliceOffsetNum;

    [NativeTypeName("bool")]
    public byte hasReferenceSlot;

    [NativeTypeName("uint32_t")]
    public uint referenceSlot;

    [NativeTypeName("const VideoH264DecodeReferenceDesc *")]
    public VideoH264DecodeReferenceDesc* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;
}
