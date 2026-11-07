namespace SharpNRI.Native;

/// <summary>A convenient way to fit pipeline layout settings into the device limits, respecting various restrictions</summary>
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

    /// <summary>uses "DeviceDesc::descriptorHeap" limits and fits descriptor sets and ranges to 0</summary>
    [NativeTypeName("bool")]
    public byte useDescriptorHeap;

    /// <summary>D3D12 only (see "NRI.hlsl" for more details)</summary>
    [NativeTypeName("bool")]
    public byte enableD3D12DrawParametersEmulation;

    [NativeTypeName("bool")]
    public byte enableD3D12DrawIndexEmulation;
}
