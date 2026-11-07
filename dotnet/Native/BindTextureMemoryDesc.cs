namespace SharpNRI.Native;

public unsafe partial struct BindTextureMemoryDesc
{
    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::Memory *")]
    public Memory* memory;

    /// <summary>in memory</summary>
    [NativeTypeName("uint64_t")]
    public ulong offset;
}
