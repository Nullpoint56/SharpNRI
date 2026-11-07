namespace SharpNRI.Native;

public unsafe partial struct VideoAV1ReferenceDesc
{
    /// <summary>NONE: a valid reference frame map entry ("ref_frame_idx") not used by the current frame; decode requires the complete map</summary>
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

    /// <summary>if provided, must include 8 entries</summary>
    [NativeTypeName("const uint8_t *")]
    public byte* savedOrderHints;
}
