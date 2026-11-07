namespace SharpNRI.Native;

/// <summary>Threadsafe: yes, except writes to overlapping descriptor indices require external synchronization</summary>
public unsafe partial struct DescriptorHeapInterface
{
    /// <summary>Create</summary>
    [NativeTypeName("Result (*)(Device &, const DescriptorHeapDesc &, DescriptorHeap *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DescriptorHeapDesc*, DescriptorHeap**, Result> CreateDescriptorHeap;

    /// <summary>Destroy</summary>
    [NativeTypeName("void (*)(DescriptorHeap *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorHeap*, void> DestroyDescriptorHeap;

    /// <summary>Updates (on HOST) GPU-side updates are not supported because D3D12 has no equivalent functionality</summary>
    [NativeTypeName("Result (*)(DescriptorHeap &, const WriteResourceDescriptorsDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorHeap*, WriteResourceDescriptorsDesc*, uint, Result> WriteResourceDescriptors;

    [NativeTypeName("Result (*)(DescriptorHeap &, const WriteSamplerDescriptorsDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorHeap*, WriteSamplerDescriptorsDesc*, uint, Result> WriteSamplerDescriptors;

    /// <summary>Command buffer {</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const DescriptorHeap &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DescriptorHeap*, void> CmdSetDescriptorHeap;
}
