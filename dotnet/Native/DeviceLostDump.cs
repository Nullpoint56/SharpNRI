namespace PrismNRI.Native;

public unsafe partial struct DeviceLostDump
{
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint64_t")]
    public ulong size;
}
