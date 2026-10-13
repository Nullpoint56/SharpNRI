namespace PrismNRI.Native;

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

    [NativeTypeName("const PipelineCache *")]
    public PipelineCache* cache;
}
