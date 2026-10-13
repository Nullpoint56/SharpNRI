namespace PrismNRI.Native;

public partial struct PipelineVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkPipeline;

    [NativeTypeName("VKEnum")]
    public int vkPipelineBindPoint;
}
