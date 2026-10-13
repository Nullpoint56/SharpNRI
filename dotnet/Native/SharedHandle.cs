namespace PrismNRI.Native;

public unsafe partial struct SharedHandle
{
    public void* handle;

    [NativeTypeName("nri::SharedHandleType")]
    public SharedHandleType type;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint64_t")]
    public ulong offset;
}
