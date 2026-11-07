namespace SharpNRI.Native;

/// <summary>https://learn.microsoft.com/en-us/windows/win32/direct3d12/root-signature-limits#static-samplers aka static (immutable) sampler</summary>
public partial struct RootSamplerDesc
{
    [NativeTypeName("uint32_t")]
    public uint registerIndex;

    [NativeTypeName("nri::SamplerDesc")]
    public SamplerDesc desc;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;
}
