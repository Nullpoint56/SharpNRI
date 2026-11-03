namespace SharpNRI.Native;

public unsafe partial struct PipelineLayoutDesc
{
    [NativeTypeName("uint32_t")]
    public uint rootRegisterSpace;

    [NativeTypeName("const RootConstantDesc *")]
    public RootConstantDesc* rootConstants;

    [NativeTypeName("uint32_t")]
    public uint rootConstantNum;

    [NativeTypeName("const RootDescriptorDesc *")]
    public RootDescriptorDesc* rootDescriptors;

    [NativeTypeName("uint32_t")]
    public uint rootDescriptorNum;

    [NativeTypeName("const RootSamplerDesc *")]
    public RootSamplerDesc* rootSamplers;

    [NativeTypeName("uint32_t")]
    public uint rootSamplerNum;

    [NativeTypeName("const DescriptorSetDesc *")]
    public DescriptorSetDesc* descriptorSets;

    [NativeTypeName("uint32_t")]
    public uint descriptorSetNum;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;

    [NativeTypeName("nri::PipelineLayoutBits")]
    public PipelineLayoutBits flags;
}
