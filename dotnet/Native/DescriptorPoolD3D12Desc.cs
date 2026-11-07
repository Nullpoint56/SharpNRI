namespace SharpNRI.Native;

public unsafe partial struct DescriptorPoolD3D12Desc
{
    public ID3D12DescriptorHeap* d3d12ResourceDescriptorHeap;

    public ID3D12DescriptorHeap* d3d12SamplerDescriptorHeap;

    /// <summary>Allocation limits (D3D12 unrelated, but must match expected usage)</summary>
    [NativeTypeName("uint32_t")]
    public uint descriptorSetMaxNum;
}
