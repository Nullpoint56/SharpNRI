namespace PrismNRI.Native;

public partial struct StreamerDesc
{
    [NativeTypeName("nri::MemoryLocation")]
    public MemoryLocation constantBufferMemoryLocation;

    [NativeTypeName("uint64_t")]
    public ulong constantBufferSize;

    [NativeTypeName("nri::MemoryLocation")]
    public MemoryLocation dynamicBufferMemoryLocation;

    [NativeTypeName("nri::BufferDesc")]
    public BufferDesc dynamicBufferDesc;

    [NativeTypeName("uint32_t")]
    public uint queuedFrameNum;

    [NativeTypeName("uint64_t")]
    public ulong hostDataCapacity;
}
