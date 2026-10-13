namespace PrismNRI.Native;

public partial struct PipelineLayoutSettingsDesc
{
    [NativeTypeName("uint32_t")]
    public uint descriptorSetNum;

    [NativeTypeName("uint32_t")]
    public uint descriptorRangeNum;

    [NativeTypeName("uint32_t")]
    public uint rootConstantSize;

    [NativeTypeName("uint32_t")]
    public uint rootDescriptorNum;

    [NativeTypeName("uint32_t")]
    public uint rootSamplerNum;

    [NativeTypeName("bool")]
    public byte preferRootDescriptorsOverConstants;

    [NativeTypeName("bool")]
    public byte useDescriptorHeap;

    [NativeTypeName("bool")]
    public byte enableD3D12DrawParametersEmulation;

    [NativeTypeName("bool")]
    public byte enableD3D12DrawIndexEmulation;
}
