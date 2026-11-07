namespace SharpNRI.Native;

public unsafe partial struct ComputePipelineDesc
{
    [NativeTypeName("const PipelineLayout *")]
    public PipelineLayout* pipelineLayout;

    [NativeTypeName("nri::ShaderDesc")]
    public ShaderDesc shader;

    [NativeTypeName("nri::ComputePipelineBits")]
    public ComputePipelineBits flags;

    [NativeTypeName("nri::Robustness")]
    public Robustness robustness;

    /// <summary>uses a cached blob on a hit and stores the result on a miss</summary>
    [NativeTypeName("const PipelineCache *")]
    public PipelineCache* cache;
}
