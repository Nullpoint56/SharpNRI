namespace SharpNRI.Native;

public unsafe partial struct DescriptorPoolD3D12Desc
{
    public ID3D12DescriptorHeap* d3d12ResourceDescriptorHeap;

    public ID3D12DescriptorHeap* d3d12SamplerDescriptorHeap;

    [NativeTypeName("uint32_t")]
    public uint descriptorSetMaxNum;
}
