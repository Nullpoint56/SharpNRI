namespace SharpNRI.Native;

public unsafe partial struct BottomLevelTrianglesDesc
{
    /// <summary>Vertices</summary>
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

    /// <summary>Indices</summary>
    [NativeTypeName("const Buffer *")]
    public Buffer* indexBuffer;

    [NativeTypeName("uint64_t")]
    public ulong indexOffset;

    [NativeTypeName("uint32_t")]
    public uint indexNum;

    [NativeTypeName("nri::IndexType")]
    public IndexType indexType;

    /// <summary>Transform contains "TransformMatrix" entries</summary>
    [NativeTypeName("const Buffer *")]
    public Buffer* transformBuffer;

    [NativeTypeName("uint64_t")]
    public ulong transformOffset;

    /// <summary>Micromap</summary>
    [NativeTypeName("nri::BottomLevelTrianglesMicromapDesc *")]
    public BottomLevelTrianglesMicromapDesc* micromap;
}
