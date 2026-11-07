namespace SharpNRI.Native;

public unsafe partial struct MicromapDesc
{
    /// <summary>can be retrieved by "CmdWriteMicromapSizes" and used for compaction via "CmdCopyMicromap"</summary>
    [NativeTypeName("uint64_t")]
    public ulong optimizedSize;

    [NativeTypeName("const MicromapUsageDesc *")]
    public MicromapUsageDesc* usages;

    [NativeTypeName("uint32_t")]
    public uint usageNum;

    [NativeTypeName("nri::MicromapBits")]
    public MicromapBits flags;
}
