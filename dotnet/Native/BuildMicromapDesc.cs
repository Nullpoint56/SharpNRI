namespace SharpNRI.Native;

public unsafe partial struct BuildMicromapDesc
{
    [NativeTypeName("nri::Micromap *")]
    public Micromap* dst;

    [NativeTypeName("const Buffer *")]
    public Buffer* dataBuffer;

    [NativeTypeName("uint64_t")]
    public ulong dataOffset;

    [NativeTypeName("const Buffer *")]
    public Buffer* triangleBuffer;

    [NativeTypeName("uint64_t")]
    public ulong triangleOffset;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* scratchBuffer;

    [NativeTypeName("uint64_t")]
    public ulong scratchOffset;
}
