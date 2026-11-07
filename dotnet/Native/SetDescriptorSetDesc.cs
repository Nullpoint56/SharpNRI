namespace SharpNRI.Native;

/// <summary>Binding</summary>
public unsafe partial struct SetDescriptorSetDesc
{
    /// <summary>an index in "PipelineLayoutDesc::descriptorSets"</summary>
    [NativeTypeName("uint32_t")]
    public uint setIndex;

    [NativeTypeName("const DescriptorSet *")]
    public DescriptorSet* descriptorSet;

    [NativeTypeName("nri::BindPoint")]
    public BindPoint bindPoint;
}
