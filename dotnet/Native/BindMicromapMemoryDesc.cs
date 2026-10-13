namespace PrismNRI.Native;

public unsafe partial struct BindMicromapMemoryDesc
{
    [NativeTypeName("nri::Micromap *")]
    public Micromap* micromap;

    [NativeTypeName("nri::Memory *")]
    public Memory* memory;

    [NativeTypeName("uint64_t")]
    public ulong offset;
}
