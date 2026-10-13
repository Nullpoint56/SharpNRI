namespace PrismNRI.Native;

public partial struct MemoryDesc
{
    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint32_t")]
    public uint alignment;

    [NativeTypeName("nri::MemoryType")]
    public uint type;

    [NativeTypeName("bool")]
    public byte mustBeDedicated;
}
