namespace PrismNRI.Native;

public partial struct RootDescriptorDesc
{
    [NativeTypeName("uint32_t")]
    public uint registerIndex;

    [NativeTypeName("nri::DescriptorType")]
    public DescriptorType descriptorType;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;
}
