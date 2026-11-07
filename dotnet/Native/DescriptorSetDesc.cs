namespace SharpNRI.Native;

/// <summary>"DescriptorSet" consists of "DescriptorRange" entities</summary>
public unsafe partial struct DescriptorSetDesc
{
    /// <summary>must be unique, avoid big gaps</summary>
    [NativeTypeName("uint32_t")]
    public uint registerSpace;

    [NativeTypeName("const DescriptorRangeDesc *")]
    public DescriptorRangeDesc* ranges;

    [NativeTypeName("uint32_t")]
    public uint rangeNum;
}
