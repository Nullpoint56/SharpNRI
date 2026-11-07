namespace SharpNRI.Native;

/// <summary>A collection of queues of the same type</summary>
public partial struct QueueFamilyVKDesc
{
    [NativeTypeName("uint32_t")]
    public uint queueNum;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;

    [NativeTypeName("uint32_t")]
    public uint familyIndex;
}
