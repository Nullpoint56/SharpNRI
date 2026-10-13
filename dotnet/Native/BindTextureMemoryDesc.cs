namespace PrismNRI.Native;

public unsafe partial struct BindTextureMemoryDesc
{
    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::Memory *")]
    public Memory* memory;

    [NativeTypeName("uint64_t")]
    public ulong offset;
}
