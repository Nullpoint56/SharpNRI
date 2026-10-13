namespace PrismNRI.Native;

public unsafe partial struct VideoAV1ReferenceDesc
{
    [NativeTypeName("nri::VideoAV1ReferenceName")]
    public VideoAV1ReferenceName name;

    [NativeTypeName("uint8_t")]
    public byte refFrameIndex;

    [NativeTypeName("nri::VideoFrameType")]
    public VideoFrameType frameType;

    [NativeTypeName("uint8_t")]
    public byte orderHint;

    [NativeTypeName("uint32_t")]
    public uint frameId;

    [NativeTypeName("uint32_t")]
    public uint slot;

    [NativeTypeName("const uint8_t *")]
    public byte* savedOrderHints;
}
