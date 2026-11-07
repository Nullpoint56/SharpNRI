namespace SharpNRI.Native;

public unsafe partial struct VideoAV1EncodeDecodeInfoDesc
{
    [NativeTypeName("const VideoEncodeFeedback *")]
    public VideoEncodeFeedback* feedback;

    [NativeTypeName("const VideoAV1SequenceDesc *")]
    public VideoAV1SequenceDesc* sequence;

    /// <summary>encoded payload prefix sufficient to parse the frame header and locate every tile; may be the complete payload. Payload-header input may be unsupported; returned bitstream ranges are bounded by "feedback"</summary>
    [NativeTypeName("const uint8_t *")]
    public byte* encodedPayloadHeader;

    [NativeTypeName("uint64_t")]
    public ulong encodedPayloadHeaderSize;

    /// <summary>previous AV1 DPB snapshot; required for inter frames, without it the frame is a key frame (as encoded with "FORCE_KEY_FRAME")</summary>
    [NativeTypeName("const VideoAV1ReferenceDesc *")]
    public VideoAV1ReferenceDesc* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;

    /// <summary>submitted picture description; required if "encodedPayloadHeader" is not provided</summary>
    [NativeTypeName("const VideoEncodePictureDesc *")]
    public VideoEncodePictureDesc* pictureDesc;

    /// <summary>submitted AV1 picture description; required if "encodedPayloadHeader" is not provided</summary>
    [NativeTypeName("const VideoAV1EncodePictureDesc *")]
    public VideoAV1EncodePictureDesc* av1PictureDesc;

    /// <summary>8 entries indexed by "refFrameIndex": the "savedFrameState" of the frame that last refreshed each slot; required for headers that inherit state</summary>
    [NativeTypeName("const VideoAV1SavedFrameState *")]
    public VideoAV1SavedFrameState* savedFrameStates;
}
