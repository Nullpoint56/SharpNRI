namespace SharpNRI.Native;

public partial struct VideoH264PictureParameterSetDesc
{
    [NativeTypeName("nri::VideoH264PictureParameterSetBits")]
    public VideoH264PictureParameterSetBits flags;

    [NativeTypeName("uint8_t")]
    public byte sequenceParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte pictureParameterSetId;

    [NativeTypeName("uint8_t")]
    public byte refIndexL0DefaultActiveMinus1;

    [NativeTypeName("uint8_t")]
    public byte refIndexL1DefaultActiveMinus1;

    [NativeTypeName("uint8_t")]
    public byte weightedBipredIdc;

    [NativeTypeName("int8_t")]
    public sbyte pictureInitQpMinus26;

    [NativeTypeName("int8_t")]
    public sbyte pictureInitQsMinus26;

    [NativeTypeName("int8_t")]
    public sbyte chromaQpIndexOffset;

    [NativeTypeName("int8_t")]
    public sbyte secondChromaQpIndexOffset;
}
