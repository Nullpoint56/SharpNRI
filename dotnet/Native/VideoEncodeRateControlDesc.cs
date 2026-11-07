namespace SharpNRI.Native;

public partial struct VideoEncodeRateControlDesc
{
    [NativeTypeName("nri::VideoEncodeRateControlMode")]
    public VideoEncodeRateControlMode mode;

    [NativeTypeName("uint8_t")]
    public byte qpI;

    [NativeTypeName("uint8_t")]
    public byte qpP;

    [NativeTypeName("uint8_t")]
    public byte qpB;

    /// <summary>CBR/VBR QP range (AV1: Q index); omitted bounds use the supported codec limits</summary>
    [NativeTypeName("uint8_t")]
    public byte qpMin;

    [NativeTypeName("uint8_t")]
    public byte qpMax;

    [NativeTypeName("uint32_t")]
    public uint frameRateNumerator;

    [NativeTypeName("uint32_t")]
    public uint frameRateDenominator;

    /// <summary>CBR target bitrate, or VBR average bitrate</summary>
    [NativeTypeName("uint64_t")]
    public ulong targetBitrate;

    /// <summary>VBR peak bitrate; defaults to "targetBitrate"</summary>
    [NativeTypeName("uint64_t")]
    public ulong maxBitrate;

    [NativeTypeName("uint64_t")]
    public ulong maxFrameBitSize;

    [NativeTypeName("uint32_t")]
    public uint virtualBufferSizeMs;

    [NativeTypeName("uint32_t")]
    public uint initialVirtualBufferSizeMs;
}
