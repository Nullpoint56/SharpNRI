namespace SharpNRI.Native;

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

    /// <summary>desired chunk size (but can be greater if a resource doesn't fit), 256 Mb if 0</summary>
    [NativeTypeName("uint64_t")]
    public ulong preferredMemorySize;

    /// <summary>[-1; 1]: low &lt; 0, normal = 0, high &gt; 0</summary>
    public float residencyPriority;

    /// <summary>memory allocation goes through "AMD Virtual Memory Allocator"</summary>
    [NativeTypeName("bool")]
    public byte vma;
}
