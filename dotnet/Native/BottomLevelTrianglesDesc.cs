namespace PrismNRI.Native;

public unsafe partial struct BottomLevelTrianglesDesc
{
    [NativeTypeName("const Buffer *")]
    public Buffer* vertexBuffer;

    [NativeTypeName("uint64_t")]
    public ulong vertexOffset;

    [NativeTypeName("uint32_t")]
    public uint vertexNum;

    [NativeTypeName("uint16_t")]
    public ushort vertexStride;

    [NativeTypeName("nri::Format")]
    public Format vertexFormat;

    [NativeTypeName("const Buffer *")]
    public Buffer* indexBuffer;

    [NativeTypeName("uint64_t")]
    public ulong indexOffset;

    [NativeTypeName("uint32_t")]
    public uint indexNum;

    [NativeTypeName("nri::IndexType")]
    public IndexType indexType;

    [NativeTypeName("const Buffer *")]
    public Buffer* transformBuffer;

    [NativeTypeName("uint64_t")]
    public ulong transformOffset;

    [NativeTypeName("nri::BottomLevelTrianglesMicromapDesc *")]
    public BottomLevelTrianglesMicromapDesc* micromap;
}
