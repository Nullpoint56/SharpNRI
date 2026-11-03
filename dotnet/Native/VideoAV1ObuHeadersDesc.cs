namespace SharpNRI.Native;

public unsafe partial struct VideoAV1ObuHeadersDesc
{
    [NativeTypeName("nri::VideoAV1SequenceDesc")]
    public VideoAV1SequenceDesc sequence;

    [NativeTypeName("uint8_t *")]
    public byte* dst;

    [NativeTypeName("uint64_t")]
    public ulong dstSize;

    [NativeTypeName("uint64_t")]
    public ulong writtenSize;
}
