namespace SharpNRI.Native;

public partial struct PipelineStatisticsDesc
{
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

    [NativeTypeName("uint64_t")]
    public ulong taskShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong meshShaderInvocationNum;

    [NativeTypeName("uint64_t")]
    public ulong meshShaderPrimitiveNum;
}
