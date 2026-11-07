namespace SharpNRI.Native;

/// <summary>Threadsafe: yes</summary>
public unsafe partial struct WrapperVKInterface
{
    [NativeTypeName("Result (*)(Device &, const CommandAllocatorVKDesc &, CommandAllocator *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, CommandAllocatorVKDesc*, CommandAllocator**, Result> CreateCommandAllocatorVK;

    [NativeTypeName("Result (*)(Device &, const CommandBufferVKDesc &, CommandBuffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, CommandBufferVKDesc*, CommandBuffer**, Result> CreateCommandBufferVK;

    [NativeTypeName("Result (*)(Device &, const DescriptorPoolVKDesc &, DescriptorPool *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DescriptorPoolVKDesc*, DescriptorPool**, Result> CreateDescriptorPoolVK;

    [NativeTypeName("Result (*)(Device &, const BufferVKDesc &, Buffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, BufferVKDesc*, Buffer**, Result> CreateBufferVK;

    [NativeTypeName("Result (*)(Device &, const TextureVKDesc &, Texture *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, TextureVKDesc*, Texture**, Result> CreateTextureVK;

    [NativeTypeName("Result (*)(Device &, const MemoryVKDesc &, Memory *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryVKDesc*, Memory**, Result> CreateMemoryVK;

    [NativeTypeName("Result (*)(Device &, const PipelineVKDesc &, Pipeline *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, PipelineVKDesc*, Pipeline**, Result> CreatePipelineVK;

    [NativeTypeName("Result (*)(Device &, const QueryPoolVKDesc &, QueryPool *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, QueryPoolVKDesc*, QueryPool**, Result> CreateQueryPoolVK;

    [NativeTypeName("Result (*)(Device &, const FenceVKDesc &, Fence *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, FenceVKDesc*, Fence**, Result> CreateFenceVK;

    [NativeTypeName("Result (*)(Device &, const AccelerationStructureVKDesc &, AccelerationStructure *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, AccelerationStructureVKDesc*, AccelerationStructure**, Result> CreateAccelerationStructureVK;

    [NativeTypeName("uint32_t (*)(const Queue &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, uint> GetQueueFamilyIndexVK;

    [NativeTypeName("VKHandle (*)(const Device &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, void*> GetPhysicalDeviceVK;

    [NativeTypeName("VKHandle (*)(const Device &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, void*> GetInstanceVK;

    [NativeTypeName("void *(*)(const Device &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, void*> GetInstanceProcAddrVK;

    [NativeTypeName("void *(*)(const Device &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, void*> GetDeviceProcAddrVK;
}
