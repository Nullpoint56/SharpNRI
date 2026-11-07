namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkSamplerCreateInfo.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_sampler_desc</summary>
public partial struct SamplerDesc
{
    [NativeTypeName("nri::Filters")]
    public Filters filters;

    [NativeTypeName("uint8_t")]
    public byte anisotropy;

    /// <summary>METAL: native shaders must apply "NriGetSamplerMipBias" (0 on Apple10+, see "NRI.metal")</summary>
    public float mipBias;

    public float mipMin;

    public float mipMax;

    [NativeTypeName("nri::AddressModes")]
    public AddressModes addressModes;

    [NativeTypeName("nri::CompareOp")]
    public CompareOp compareOp;

    /// <summary>used only with "AddressMode::CLAMP_TO_BORDER" (METAL: only transparent black, opaque black or opaque white)</summary>
    [NativeTypeName("nri::Color")]
    public Color borderColor;

    [NativeTypeName("bool")]
    public byte isInteger;

    /// <summary>requires "shaderFeatures.unnormalizedCoordinates"</summary>
    [NativeTypeName("bool")]
    public byte unnormalizedCoordinates;
}
