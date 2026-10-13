namespace PrismNRI.Native;

public partial struct QueueFamilyVKDesc
{
    [NativeTypeName("uint32_t")]
    public uint queueNum;

    [NativeTypeName("nri::QueueType")]
    public QueueType queueType;

    [NativeTypeName("uint32_t")]
    public uint familyIndex;
}
