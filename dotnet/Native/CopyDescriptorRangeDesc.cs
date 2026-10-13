namespace PrismNRI.Native;

public unsafe partial struct CopyDescriptorRangeDesc
{
    [NativeTypeName("nri::DescriptorSet *")]
    public DescriptorSet* dstDescriptorSet;

    [NativeTypeName("uint32_t")]
    public uint dstRangeIndex;

    [NativeTypeName("uint32_t")]
    public uint dstBaseDescriptor;

    [NativeTypeName("const DescriptorSet *")]
    public DescriptorSet* srcDescriptorSet;

    [NativeTypeName("uint32_t")]
    public uint srcRangeIndex;

    [NativeTypeName("uint32_t")]
    public uint srcBaseDescriptor;

    [NativeTypeName("uint32_t")]
    public uint descriptorNum;
}
