namespace PrismNRI.Native;

public partial struct Uid_t
{
    [NativeTypeName("uint64_t")]
    public ulong low;

    [NativeTypeName("uint64_t")]
    public ulong high;
}
