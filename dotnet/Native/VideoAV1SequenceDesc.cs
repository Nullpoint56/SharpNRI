namespace SharpNRI.Native;

public partial struct VideoAV1SequenceDesc
{
    [NativeTypeName("nri::VideoAV1SequenceBits")]
    public VideoAV1SequenceBits flags;

    [NativeTypeName("uint8_t")]
    public byte seqProfile;

    [NativeTypeName("uint8_t")]
    public byte bitDepth;

    [NativeTypeName("uint8_t")]
    public byte subsamplingX;

    [NativeTypeName("uint8_t")]
    public byte subsamplingY;

    [NativeTypeName("uint16_t")]
    public ushort maxFrameWidthMinus1;

    [NativeTypeName("uint16_t")]
    public ushort maxFrameHeightMinus1;

    [NativeTypeName("uint8_t")]
    public byte frameWidthBitsMinus1;

    [NativeTypeName("uint8_t")]
    public byte frameHeightBitsMinus1;

    [NativeTypeName("uint8_t")]
    public byte deltaFrameIdLengthMinus2;

    [NativeTypeName("uint8_t")]
    public byte additionalFrameIdLengthMinus1;

    [NativeTypeName("uint8_t")]
    public byte orderHintBitsMinus1;

    [NativeTypeName("uint8_t")]
    public byte seqForceIntegerMv;

    [NativeTypeName("uint8_t")]
    public byte seqForceScreenContentTools;

    [NativeTypeName("uint8_t")]
    public byte level;

    [NativeTypeName("uint8_t")]
    public byte colorPrimaries;

    [NativeTypeName("uint8_t")]
    public byte transferCharacteristics;

    [NativeTypeName("uint8_t")]
    public byte matrixCoefficients;

    [NativeTypeName("uint8_t")]
    public byte chromaSamplePosition;

    [NativeTypeName("uint32_t")]
    public uint numUnitsInDisplayTick;

    [NativeTypeName("uint32_t")]
    public uint timeScale;

    [NativeTypeName("uint32_t")]
    public uint numTicksPerPictureMinus1;
}
