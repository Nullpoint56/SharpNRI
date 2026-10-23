namespace PrismNRI.Native;

public partial struct FenceDesc
{
    [NativeTypeName("uint64_t")]
    public ulong initialValue;

    [NativeTypeName("bool")]
    public byte shared;
}
