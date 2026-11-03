namespace SharpNRI.Native;

public unsafe partial struct VideoAV1TileLayoutDesc
{
    [NativeTypeName("uint8_t")]
    public byte columnNum;

    [NativeTypeName("uint8_t")]
    public byte rowNum;

    [NativeTypeName("uint8_t")]
    public byte tileSizeBytesMinus1;

    [NativeTypeName("uint8_t")]
    public byte uniformSpacing;

    [NativeTypeName("uint16_t")]
    public ushort contextUpdateTileId;

    [NativeTypeName("const uint16_t *")]
    public ushort* miColumnStarts;

    [NativeTypeName("const uint16_t *")]
    public ushort* miRowStarts;

    [NativeTypeName("const uint16_t *")]
    public ushort* widthInSuperblocksMinus1;

    [NativeTypeName("const uint16_t *")]
    public ushort* heightInSuperblocksMinus1;
}
