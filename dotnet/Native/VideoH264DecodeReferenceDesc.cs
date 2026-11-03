namespace SharpNRI.Native;

public partial struct VideoH264DecodeReferenceDesc
{
    [NativeTypeName("nri::VideoH264DecodeReferenceBits")]
    public VideoH264DecodeReferenceBits flags;

    [NativeTypeName("uint16_t")]
    public ushort frameNum;

    [NativeTypeName("uint32_t")]
    public uint slot;

    [NativeTypeName("int32_t")]
    public int topFieldOrderCount;

    [NativeTypeName("int32_t")]
    public int bottomFieldOrderCount;
}
