namespace SharpNRI.Native;

/// <summary>Data layout for QueryType::PIPELINE_STATISTICS https://docs.vulkan.org/refpages/latest/refpages/source/VkQueryPipelineStatisticFlagBits.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_query_data_pipeline_statistics</summary>
public partial struct PipelineStatisticsDesc
{
    /// <summary>Common part</summary>
    [NativeTypeName("uint64_t")]
    public ulong inputVertexNum;

    [NativeTypeName("uint64_t")]
    public ulong inputPrimitiveNum;

    [NativeTypeName("uint64_t")]
    public ulong vertexShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong geometryShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong geometryShaderPrimitiveNum;

    [NativeTypeName("uint64_t")]
    public ulong rasterizerInPrimitiveNum;

    [NativeTypeName("uint64_t")]
    public ulong rasterizerOutPrimitiveNum;

    [NativeTypeName("uint64_t")]
    public ulong fragmentShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong tessControlShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong tessEvaluationShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong computeShaderInvocationNum;

    /// <summary>If "features.meshShaderPipelineStats"</summary>
    [NativeTypeName("uint64_t")]
    public ulong taskShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong meshShaderInvocationNum;

    /// <summary>D3D12: if "features.meshShaderPipelineStats"</summary>
    [NativeTypeName("uint64_t")]
    public ulong meshShaderPrimitiveNum;
}
