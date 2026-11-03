namespace SharpNRI.Native;

public unsafe partial struct HelperInterface
{
    [NativeTypeName("uint32_t (*)(const Device &, const ResourceGroupDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ResourceGroupDesc*, uint> CalculateAllocationNumber;

    [NativeTypeName("Result (*)(Device &, const ResourceGroupDesc &, Memory **) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ResourceGroupDesc*, Memory**, Result> AllocateAndBindMemory;

    [NativeTypeName("Result (*)(Queue &, const TextureUploadDesc *, uint32_t, const BufferUploadDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, TextureUploadDesc*, uint, BufferUploadDesc*, uint, Result> UploadData;

    [NativeTypeName("Result (*)(const Device &, MemoryLocation, VideoMemoryInfo &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryLocation, VideoMemoryInfo*, Result> QueryVideoMemoryInfo;
}
