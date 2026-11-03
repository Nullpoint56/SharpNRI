namespace SharpNRI.Native;

public unsafe partial struct SetDescriptorSetDesc
{
    [NativeTypeName("uint32_t")]
    public uint setIndex;

    [NativeTypeName("const DescriptorSet *")]
    public DescriptorSet* descriptorSet;

    [NativeTypeName("nri::BindPoint")]
    public BindPoint bindPoint;
}
