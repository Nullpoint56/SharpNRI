namespace SharpNRI.Native;

public partial struct FenceVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkTimelineSemaphore;
}
