namespace PrismNRI.Native;

public unsafe partial struct BindBufferMemoryDesc
{
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::Memory *")]
    public Memory* memory;

    [NativeTypeName("uint64_t")]
    public ulong offset;
}
