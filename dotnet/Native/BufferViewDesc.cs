namespace SharpNRI.Native;

public unsafe partial struct BufferViewDesc
{
    [NativeTypeName("const Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::BufferView")]
    public BufferView type;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("nri::Format")]
    public Format format;

    [NativeTypeName("uint32_t")]
    public uint structureStride;
}
