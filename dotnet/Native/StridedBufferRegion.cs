namespace PrismNRI.Native;

public unsafe partial struct StridedBufferRegion
{
    [NativeTypeName("const Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint64_t")]
    public ulong stride;
}
