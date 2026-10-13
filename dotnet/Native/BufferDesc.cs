namespace PrismNRI.Native;

public partial struct BufferDesc
{
    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint32_t")]
    public uint structureStride;

    [NativeTypeName("nri::BufferUsageBits")]
    public BufferUsageBits usage;

    [NativeTypeName("bool")]
    public byte byteAddress;
}
