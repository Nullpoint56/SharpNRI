namespace SharpNRI.Native;

public unsafe partial struct VideoBitstreamRange
{
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    /// <summary>decode slice/tile offsets are relative to this offset</summary>
    [NativeTypeName("uint64_t")]
    public ulong offset;

    /// <summary>decode bitstream byte range; H.264/H.265 neutral decode expects Annex-B byte stream data</summary>
    [NativeTypeName("uint64_t")]
    public ulong size;
}
