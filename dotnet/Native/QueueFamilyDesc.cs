namespace SharpNRI.Native;

/// <summary>A collection of queues of the same type</summary>
public unsafe partial struct QueueFamilyDesc
{
    /// <summary>[-1; 1]: low &lt; 0, normal = 0, high &gt; 0 ("queueNum" entries expected)</summary>
    [NativeTypeName("const float *")]
    public float* queuePriorities;

    [NativeTypeName("uint32_t")]
    public uint queueNum;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;
}
