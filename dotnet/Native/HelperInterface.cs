namespace SharpNRI.Native;

/// <summary>Threadsafe: yes</summary>
public unsafe partial struct HelperInterface
{
    /// <summary>Optimized memory allocation for a group of resources</summary>
    [NativeTypeName("uint32_t (*)(const Device &, const ResourceGroupDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ResourceGroupDesc*, uint> CalculateAllocationNumber;

    /// <summary>"allocations" must have entries &gt;= returned by "CalculateAllocationNumber"</summary>
    [NativeTypeName("Result (*)(Device &, const ResourceGroupDesc &, Memory **) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ResourceGroupDesc*, Memory**, Result> AllocateAndBindMemory;

    /// <summary>Populate resources with data (not for streaming!)</summary>
    [NativeTypeName("Result (*)(Queue &, const TextureUploadDesc *, uint32_t, const BufferUploadDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, TextureUploadDesc*, uint, BufferUploadDesc*, uint, Result> UploadData;

    /// <summary>Information about video memory</summary>
    [NativeTypeName("Result (*)(const Device &, MemoryLocation, VideoMemoryInfo &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryLocation, VideoMemoryInfo*, Result> QueryVideoMemoryInfo;
}
