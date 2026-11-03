namespace SharpNRI.Native;

public partial struct VideoMemoryInfo
{
    [NativeTypeName("uint64_t")]
    public ulong budgetSize;

    [NativeTypeName("uint64_t")]
    public ulong usageSize;
}
