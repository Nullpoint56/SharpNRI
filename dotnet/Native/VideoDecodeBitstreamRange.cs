namespace SharpNRI.Native;

public unsafe partial struct VideoDecodeBitstreamRange
{
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    /// <summary>host range starts at this address; "offset" applies only to "buffer"</summary>
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    /// <summary>H.264/H.265 neutral decode expects Annex-B byte stream data</summary>
    [NativeTypeName("uint64_t")]
    public ulong size;
}
