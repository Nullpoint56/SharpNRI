namespace PrismNRI.Native;

public unsafe partial struct WrapperD3D12Interface
{
    [NativeTypeName("Result (*)(Device &, const CommandBufferD3D12Desc &, CommandBuffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, CommandBufferD3D12Desc*, CommandBuffer**, Result> CreateCommandBufferD3D12;

    [NativeTypeName("Result (*)(Device &, const DescriptorPoolD3D12Desc &, DescriptorPool *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DescriptorPoolD3D12Desc*, DescriptorPool**, Result> CreateDescriptorPoolD3D12;

    [NativeTypeName("Result (*)(Device &, const BufferD3D12Desc &, Buffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, BufferD3D12Desc*, Buffer**, Result> CreateBufferD3D12;

    [NativeTypeName("Result (*)(Device &, const TextureD3D12Desc &, Texture *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, TextureD3D12Desc*, Texture**, Result> CreateTextureD3D12;

    [NativeTypeName("Result (*)(Device &, const MemoryD3D12Desc &, Memory *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryD3D12Desc*, Memory**, Result> CreateMemoryD3D12;

    [NativeTypeName("Result (*)(Device &, const FenceD3D12Desc &, Fence *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, FenceD3D12Desc*, Fence**, Result> CreateFenceD3D12;

    [NativeTypeName("Result (*)(Device &, const AccelerationStructureD3D12Desc &, AccelerationStructure *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, AccelerationStructureD3D12Desc*, AccelerationStructure**, Result> CreateAccelerationStructureD3D12;
}
