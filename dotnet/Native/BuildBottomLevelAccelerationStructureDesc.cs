namespace PrismNRI.Native;

public unsafe partial struct BuildBottomLevelAccelerationStructureDesc
{
    [NativeTypeName("nri::AccelerationStructure *")]
    public AccelerationStructure* dst;

    [NativeTypeName("const AccelerationStructure *")]
    public AccelerationStructure* src;

    [NativeTypeName("const BottomLevelGeometryDesc *")]
    public BottomLevelGeometryDesc* geometries;

    [NativeTypeName("uint32_t")]
    public uint geometryNum;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* scratchBuffer;

    [NativeTypeName("uint64_t")]
    public ulong scratchOffset;
}
