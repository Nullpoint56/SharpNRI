namespace SharpNRI.Native;

public unsafe partial struct BottomLevelAabbsDesc
{
    /// <summary>contains "BottomLevelAabb" entries</summary>
    [NativeTypeName("const Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    [NativeTypeName("uint32_t")]
    public uint num;

    [NativeTypeName("uint32_t")]
    public uint stride;
}
