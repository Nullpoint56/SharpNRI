namespace PrismNRI.Native;

public unsafe partial struct VertexBufferDesc
{
    [NativeTypeName("const Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    [NativeTypeName("uint32_t")]
    public uint stride;
}
