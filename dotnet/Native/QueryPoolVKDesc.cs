namespace PrismNRI.Native;

public partial struct QueryPoolVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkQueryPool;

    [NativeTypeName("VKEnum")]
    public int vkQueryType;
}
