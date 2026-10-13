namespace PrismNRI.Native;

public unsafe partial struct QueueFamilyD3D12Desc
{
    [NativeTypeName("ID3D12CommandQueue *const *")]
    public ID3D12CommandQueue** d3d12Queues;

    [NativeTypeName("uint32_t")]
    public uint queueNum;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;
}
