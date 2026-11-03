namespace SharpNRI.Native;

public unsafe partial struct StreamBufferDataDesc
{
    [NativeTypeName("const DataSize *")]
    public DataSize* dataChunks;

    [NativeTypeName("uint32_t")]
    public uint dataChunkNum;

    [NativeTypeName("uint32_t")]
    public uint placementAlignment;

    [NativeTypeName("nri::StreamerCopyBatch")]
    public ulong copyBatch;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* dstBuffer;

    [NativeTypeName("uint64_t")]
    public ulong dstOffset;
}
