namespace SharpNRI.Native;

/// <summary>A collection of queues of the same type</summary>
public unsafe partial struct QueueFamilyD3D12Desc
{
    /// <summary>if not provided, will be created</summary>
    [NativeTypeName("ID3D12CommandQueue *const *")]
    public ID3D12CommandQueue** d3d12Queues;

    [NativeTypeName("uint32_t")]
    public uint queueNum;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;
}
