namespace PrismNRI.Native;

public unsafe partial struct UpdateDescriptorRangeDesc
{
    [NativeTypeName("nri::DescriptorSet *")]
    public DescriptorSet* descriptorSet;

    [NativeTypeName("uint32_t")]
    public uint rangeIndex;

    [NativeTypeName("uint32_t")]
    public uint baseDescriptor;

    [NativeTypeName("const Descriptor *const *")]
    public Descriptor** descriptors;

    [NativeTypeName("uint32_t")]
    public uint descriptorNum;
}
