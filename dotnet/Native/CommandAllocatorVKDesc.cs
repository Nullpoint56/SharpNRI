namespace PrismNRI.Native;

public partial struct CommandAllocatorVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkCommandPool;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;
}
