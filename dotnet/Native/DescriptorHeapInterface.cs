namespace PrismNRI.Native;

public unsafe partial struct DescriptorHeapInterface
{
    [NativeTypeName("Result (*)(Device &, const DescriptorHeapDesc &, DescriptorHeap *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DescriptorHeapDesc*, DescriptorHeap**, Result> CreateDescriptorHeap;

    [NativeTypeName("void (*)(DescriptorHeap *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorHeap*, void> DestroyDescriptorHeap;

    [NativeTypeName("Result (*)(DescriptorHeap &, const WriteResourceDescriptorsDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorHeap*, WriteResourceDescriptorsDesc*, uint, Result> WriteResourceDescriptors;

    [NativeTypeName("Result (*)(DescriptorHeap &, const WriteSamplerDescriptorsDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorHeap*, WriteSamplerDescriptorsDesc*, uint, Result> WriteSamplerDescriptors;

    [NativeTypeName("void (*)(CommandBuffer &, const DescriptorHeap &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DescriptorHeap*, void> CmdSetDescriptorHeap;
}
