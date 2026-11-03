namespace SharpNRI.Native;

public partial struct DescriptorHeapDesc
{
    [NativeTypeName("uint32_t")]
    public uint resourceDescriptorNum;

    [NativeTypeName("uint32_t")]
    public uint samplerDescriptorNum;
}
