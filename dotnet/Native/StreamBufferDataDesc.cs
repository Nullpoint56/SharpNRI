namespace SharpNRI.Native;

public unsafe partial struct StreamBufferDataDesc
{
    /// <summary>Data to upload will be concatenated in dynamic buffer memory</summary>
    [NativeTypeName("const DataSize *")]
    public DataSize* dataChunks;

    [NativeTypeName("uint32_t")]
    public uint dataChunkNum;

    /// <summary>desired alignment for "BufferOffset::offset"</summary>
    [NativeTypeName("uint32_t")]
    public uint placementAlignment;

    /// <summary>Destination required if "dstBuffer" is not NULL</summary>
    [NativeTypeName("nri::StreamerCopyBatch")]
    public ulong copyBatch;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* dstBuffer;

    [NativeTypeName("uint64_t")]
    public ulong dstOffset;
}
