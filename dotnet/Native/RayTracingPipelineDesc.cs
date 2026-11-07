namespace SharpNRI.Native;

public unsafe partial struct RayTracingPipelineDesc
{
    [NativeTypeName("const PipelineLayout *")]
    public PipelineLayout* pipelineLayout;

    [NativeTypeName("const ShaderLibraryDesc *")]
    public ShaderLibraryDesc* shaderLibrary;

    [NativeTypeName("const ShaderGroupDesc *")]
    public ShaderGroupDesc* shaderGroups;

    [NativeTypeName("uint32_t")]
    public uint shaderGroupNum;

    [NativeTypeName("uint32_t")]
    public uint recursionMaxDepth;

    [NativeTypeName("uint32_t")]
    public uint rayPayloadMaxSize;

    [NativeTypeName("uint32_t")]
    public uint rayHitAttributeMaxSize;

    [NativeTypeName("nri::RayTracingPipelineBits")]
    public RayTracingPipelineBits flags;

    [NativeTypeName("nri::Robustness")]
    public Robustness robustness;

    /// <summary>uses a cached blob on a hit and stores the result on a miss</summary>
    [NativeTypeName("const PipelineCache *")]
    public PipelineCache* cache;
}
