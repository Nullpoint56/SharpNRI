namespace PrismNRI.Native;

public unsafe partial struct DescriptorSetDesc
{
    [NativeTypeName("uint32_t")]
    public uint registerSpace;

    [NativeTypeName("const DescriptorRangeDesc *")]
    public DescriptorRangeDesc* ranges;

    [NativeTypeName("uint32_t")]
    public uint rangeNum;
}
