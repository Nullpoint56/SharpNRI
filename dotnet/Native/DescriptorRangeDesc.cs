namespace SharpNRI.Native;

/// <summary>"DescriptorRange" consists of "Descriptor" entities</summary>
public partial struct DescriptorRangeDesc
{
    /// <summary>"VKBindingOffsets" not applied to "MUTABLE" and "INPUT_ATTACHMENT" to avoid confusion</summary>
    [NativeTypeName("uint32_t")]
    public uint baseRegisterIndex;

    /// <summary>treated as max size if "VARIABLE_SIZED_ARRAY" flag is set</summary>
    [NativeTypeName("uint32_t")]
    public uint descriptorNum;

    [NativeTypeName("nri::DescriptorType")]
    public DescriptorType descriptorType;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;

    [NativeTypeName("nri::DescriptorRangeBits")]
    public DescriptorRangeBits flags;
}
