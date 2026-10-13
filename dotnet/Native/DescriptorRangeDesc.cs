namespace PrismNRI.Native;

public partial struct DescriptorRangeDesc
{
    [NativeTypeName("uint32_t")]
    public uint baseRegisterIndex;

    [NativeTypeName("uint32_t")]
    public uint descriptorNum;

    [NativeTypeName("nri::DescriptorType")]
    public DescriptorType descriptorType;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;

    [NativeTypeName("nri::DescriptorRangeBits")]
    public DescriptorRangeBits flags;
}
