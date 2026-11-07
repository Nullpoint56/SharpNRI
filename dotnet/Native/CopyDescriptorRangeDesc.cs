namespace SharpNRI.Native;

/// <summary>Copying descriptors between descriptor sets</summary>
public unsafe partial struct CopyDescriptorRangeDesc
{
    /// <summary>Destination</summary>
    [NativeTypeName("nri::DescriptorSet *")]
    public DescriptorSet* dstDescriptorSet;

    [NativeTypeName("uint32_t")]
    public uint dstRangeIndex;

    [NativeTypeName("uint32_t")]
    public uint dstBaseDescriptor;

    /// <summary>Source &amp; count must be allocated from a "DescriptorPool" with "DescriptorPoolBits::COPY_SOURCE"</summary>
    [NativeTypeName("const DescriptorSet *")]
    public DescriptorSet* srcDescriptorSet;

    [NativeTypeName("uint32_t")]
    public uint srcRangeIndex;

    [NativeTypeName("uint32_t")]
    public uint srcBaseDescriptor;

    /// <summary>must be &gt; 0</summary>
    [NativeTypeName("uint32_t")]
    public uint descriptorNum;
}
