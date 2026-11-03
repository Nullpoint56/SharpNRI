namespace SharpNRI.Native;

public partial struct RootSamplerDesc
{
    [NativeTypeName("uint32_t")]
    public uint registerIndex;

    [NativeTypeName("nri::SamplerDesc")]
    public SamplerDesc desc;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;
}
