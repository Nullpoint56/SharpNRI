namespace PrismNRI.Native;

public unsafe partial struct BufferOffset
{
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("uint64_t")]
    public ulong offset;
}
