namespace SharpNRI.Native;

/// <summary>"PipelineLayout" consists of "DescriptorSet" descriptions and root parameters aka push constants block</summary>
public partial struct RootConstantDesc
{
    [NativeTypeName("uint32_t")]
    public uint registerIndex;

    /// <summary>must be non-zero and a multiple of 4</summary>
    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("nri::StageBits")]
    public StageBits shaderStages;
}
