namespace SharpNRI.Native;

public unsafe partial struct AccelerationStructureDesc
{
    /// <summary>can be retrieved by "CmdWriteAccelerationStructureSizes" and used for compaction via "CmdCopyAccelerationStructure"</summary>
    [NativeTypeName("uint64_t")]
    public ulong optimizedSize;

    /// <summary>needed only for "BOTTOM_LEVEL", "HAS_BUFFER" can be used to indicate a buffer presence (no real entities needed at initialization time)</summary>
    [NativeTypeName("const BottomLevelGeometryDesc *")]
    public BottomLevelGeometryDesc* geometries;

    [NativeTypeName("uint32_t")]
    public uint geometryOrInstanceNum;

    [NativeTypeName("nri::AccelerationStructureBits")]
    public AccelerationStructureBits flags;

    [NativeTypeName("nri::AccelerationStructureType")]
    public AccelerationStructureType type;
}
