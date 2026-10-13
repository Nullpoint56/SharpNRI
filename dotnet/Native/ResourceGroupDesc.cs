namespace PrismNRI.Native;

public unsafe partial struct ResourceGroupDesc
{
    [NativeTypeName("nri::MemoryLocation")]
    public MemoryLocation memoryLocation;

    [NativeTypeName("Texture *const *")]
    public Texture** textures;

    [NativeTypeName("uint32_t")]
    public uint textureNum;

    [NativeTypeName("Buffer *const *")]
    public Buffer** buffers;

    [NativeTypeName("uint32_t")]
    public uint bufferNum;

    [NativeTypeName("uint64_t")]
    public ulong preferredMemorySize;

    public float residencyPriority;

    [NativeTypeName("bool")]
    public byte vma;
}
