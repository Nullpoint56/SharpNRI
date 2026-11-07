namespace SharpNRI.Native;

/// <summary>Binding resources to a memory (resources can overlap, i.e. alias)</summary>
public unsafe partial struct BindBufferMemoryDesc
{
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::Memory *")]
    public Memory* memory;

    /// <summary>in memory</summary>
    [NativeTypeName("uint64_t")]
    public ulong offset;
}
