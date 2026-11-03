namespace PrismNRI.Native;

public unsafe partial struct VideoAV1EncodeDecodeInfoDesc
{
    [NativeTypeName("const VideoEncodeFeedback *")]
    public VideoEncodeFeedback* feedback;

    [NativeTypeName("const VideoAV1SequenceDesc *")]
    public VideoAV1SequenceDesc* sequence;

    [NativeTypeName("const uint8_t *")]
    public byte* encodedPayloadHeader;

    [NativeTypeName("uint64_t")]
    public ulong encodedPayloadHeaderSize;

    [NativeTypeName("const VideoAV1ReferenceDesc *")]
    public VideoAV1ReferenceDesc* references;

    [NativeTypeName("uint32_t")]
    public uint referenceNum;

    [NativeTypeName("const VideoEncodePictureDesc *")]
    public VideoEncodePictureDesc* pictureDesc;

    [NativeTypeName("const VideoAV1EncodePictureDesc *")]
    public VideoAV1EncodePictureDesc* av1PictureDesc;

    [NativeTypeName("const VideoAV1SavedFrameState *")]
    public VideoAV1SavedFrameState* savedFrameStates;
}
