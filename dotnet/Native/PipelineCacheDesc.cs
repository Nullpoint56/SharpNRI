namespace PrismNRI.Native;

public unsafe partial struct PipelineCacheDesc
{
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint64_t")]
    public ulong size;
}
