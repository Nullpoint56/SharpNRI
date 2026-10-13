namespace PrismNRI.Native;

public partial struct TextureDataLayoutDesc
{
    [NativeTypeName("uint64_t")]
    public ulong offset;

    [NativeTypeName("uint32_t")]
    public uint rowPitch;

    [NativeTypeName("uint32_t")]
    public uint slicePitch;
}
