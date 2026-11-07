namespace SharpNRI.Native;

/// <summary>Memory requirements for a resource (buffer or texture)</summary>
public partial struct MemoryDesc
{
    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint32_t")]
    public uint alignment;

    [NativeTypeName("nri::MemoryType")]
    public uint type;

    /// <summary>must be put into a dedicated "Memory" object, containing only 1 object with offset = 0</summary>
    [NativeTypeName("bool")]
    public byte mustBeDedicated;
}
