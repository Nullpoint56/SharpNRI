namespace SharpNRI.Native;

public unsafe partial struct VideoAnnexBEndOfStreamDesc
{
    [NativeTypeName("nri::VideoCodec")]
    public VideoCodec codec;

    [NativeTypeName("uint8_t *")]
    public byte* dst;

    [NativeTypeName("uint64_t")]
    public ulong dstSize;

    [NativeTypeName("uint64_t")]
    public ulong writtenSize;
}
