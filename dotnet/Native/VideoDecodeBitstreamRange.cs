namespace PrismNRI.Native;

public unsafe partial struct VideoDecodeBitstreamRange
{
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    [NativeTypeName("uint64_t")]
    public ulong size;
}
