namespace SharpNRI.Native;

/// <summary>Updating/initializing descriptors in a descriptor set</summary>
public unsafe partial struct UpdateDescriptorRangeDesc
{
    /// <summary>Destination</summary>
    [NativeTypeName("nri::DescriptorSet *")]
    public DescriptorSet* descriptorSet;

    [NativeTypeName("uint32_t")]
    public uint rangeIndex;

    [NativeTypeName("uint32_t")]
    public uint baseDescriptor;

    /// <summary>Source &amp; count all descriptors must have the same type</summary>
    [NativeTypeName("const Descriptor *const *")]
    public Descriptor** descriptors;

    [NativeTypeName("uint32_t")]
    public uint descriptorNum;
}
