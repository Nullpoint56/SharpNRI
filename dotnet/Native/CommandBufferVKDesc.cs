namespace PrismNRI.Native;

public unsafe partial struct CommandBufferVKDesc
{
    [NativeTypeName("VKHandle")]
    public void* vkCommandBuffer;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;
}
