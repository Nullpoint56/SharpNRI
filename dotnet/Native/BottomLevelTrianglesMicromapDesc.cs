namespace PrismNRI.Native;

public unsafe partial struct BottomLevelTrianglesMicromapDesc
{
    [NativeTypeName("nri::Micromap *")]
    public Micromap* micromap;

    [NativeTypeName("const Buffer *")]
    public Buffer* indexBuffer;

    [NativeTypeName("uint64_t")]
    public ulong indexOffset;

    [NativeTypeName("uint32_t")]
    public uint baseTriangle;

    [NativeTypeName("nri::IndexType")]
    public IndexType indexType;
}
