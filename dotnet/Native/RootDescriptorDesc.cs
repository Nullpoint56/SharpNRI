namespace SharpNRI.Native;

/// <summary>aka push descriptor</summary>
public partial struct RootDescriptorDesc
{
    [NativeTypeName("uint32_t")]
    public uint registerIndex;

    /// <summary>a non-typed descriptor type</summary>
    [NativeTypeName("nri::DescriptorType")]
    public DescriptorType descriptorType;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;
}
