namespace SharpNRI.Native;

public partial struct VideoMemoryInfo
{
    /// <summary>the OS-provided video memory budget. If "usageSize" &gt; "budgetSize", the application may incur stuttering or performance penalties</summary>
    [NativeTypeName("uint64_t")]
    public ulong budgetSize;

    /// <summary>specifies the application’s current video memory usage</summary>
    [NativeTypeName("uint64_t")]
    public ulong usageSize;
}
