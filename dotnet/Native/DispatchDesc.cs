namespace SharpNRI.Native;

public partial struct DispatchDesc
{
    [NativeTypeName("uint32_t")]
    public uint workGroupNumX;

    [NativeTypeName("uint32_t")]
    public uint workGroupNumY;

    [NativeTypeName("uint32_t")]
    public uint workGroupNumZ;
}
