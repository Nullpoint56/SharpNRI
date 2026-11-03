namespace SharpNRI.Native;

public unsafe partial struct GraphicsPipelineDesc
{
    [NativeTypeName("const PipelineLayout *")]
    public PipelineLayout* pipelineLayout;

    [NativeTypeName("const VertexInputDesc *")]
    public VertexInputDesc* vertexInput;

    [NativeTypeName("nri::InputAssemblyDesc")]
    public InputAssemblyDesc inputAssembly;

    [NativeTypeName("nri::RasterizationDesc")]
    public RasterizationDesc rasterization;

    [NativeTypeName("const MultisampleDesc *")]
    public MultisampleDesc* multisample;

    [NativeTypeName("nri::OutputMergerDesc")]
    public OutputMergerDesc outputMerger;

    [NativeTypeName("const ShaderDesc *")]
    public ShaderDesc* shaders;

    [NativeTypeName("uint32_t")]
    public uint shaderNum;

    [NativeTypeName("nri::GraphicsPipelineBits")]
    public GraphicsPipelineBits flags;

    [NativeTypeName("nri::Robustness")]
    public Robustness robustness;

    [NativeTypeName("const PipelineCache *")]
    public PipelineCache* cache;
}
