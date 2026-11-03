namespace SharpNRI.Native;

public unsafe partial struct AccelerationStructureDesc
{
    [NativeTypeName("uint64_t")]
    public ulong optimizedSize;

    [NativeTypeName("const BottomLevelGeometryDesc *")]
    public BottomLevelGeometryDesc* geometries;

    [NativeTypeName("uint32_t")]
    public uint geometryOrInstanceNum;

    [NativeTypeName("nri::AccelerationStructureBits")]
    public AccelerationStructureBits flags;

    [NativeTypeName("nri::AccelerationStructureType")]
    public AccelerationStructureType type;
}
