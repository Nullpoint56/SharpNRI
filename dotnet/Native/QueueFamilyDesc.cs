namespace PrismNRI.Native;

public unsafe partial struct QueueFamilyDesc
{
    [NativeTypeName("const float *")]
    public float* queuePriorities;

    [NativeTypeName("uint32_t")]
    public uint queueNum;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;
}
