namespace SharpNRI.Native;

public unsafe partial struct VideoAV1TileLayoutDesc
{
    [NativeTypeName("uint8_t")]
    public byte columnNum;

    [NativeTypeName("uint8_t")]
    public byte rowNum;

    [NativeTypeName("uint8_t")]
    public byte tileSizeBytesMinus1;

    /// <summary>if set, omitted arrays are derived from the frame size</summary>
    [NativeTypeName("uint8_t")]
    public byte uniformSpacing;

    [NativeTypeName("uint16_t")]
    public ushort contextUpdateTileId;

    /// <summary>if provided, must include "columnNum + 1" entries</summary>
    [NativeTypeName("const uint16_t *")]
    public ushort* miColumnStarts;

    /// <summary>if provided, must include "rowNum + 1" entries</summary>
    [NativeTypeName("const uint16_t *")]
    public ushort* miRowStarts;

    /// <summary>if provided, must include "columnNum" entries</summary>
    [NativeTypeName("const uint16_t *")]
    public ushort* widthInSuperblocksMinus1;

    /// <summary>if provided, must include "rowNum" entries</summary>
    [NativeTypeName("const uint16_t *")]
    public ushort* heightInSuperblocksMinus1;
}
