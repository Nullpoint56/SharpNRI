namespace SharpNRI.Native;

public unsafe partial struct PipelineCacheDesc
{
    /// <summary>"data = NULL" means empty cache</summary>
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint64_t")]
    public ulong size;
}
