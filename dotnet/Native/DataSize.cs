namespace SharpNRI.Native;

public unsafe partial struct DataSize
{
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint64_t")]
    public ulong size;
}
