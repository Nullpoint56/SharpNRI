namespace SharpNRI.Native;

public partial struct VideoAV1DecodeTileDesc
{
    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("uint16_t")]
    public ushort row;

    [NativeTypeName("uint16_t")]
    public ushort column;

    [NativeTypeName("uint8_t")]
    public byte anchorFrame;
}
