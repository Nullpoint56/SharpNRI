namespace PrismNRI.Native;

public partial struct RootConstantDesc
{
    [NativeTypeName("uint32_t")]
    public uint registerIndex;

    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;
}
