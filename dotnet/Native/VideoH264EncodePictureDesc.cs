namespace SharpNRI.Native;

public unsafe partial struct VideoH264EncodePictureDesc
{
    [NativeTypeName("uint8_t")]
    public byte sequenceParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte pictureParameterSetId;

    [NativeTypeName("const VideoH264EncodeReferenceDesc *")]
    public VideoH264EncodeReferenceDesc* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;
}
