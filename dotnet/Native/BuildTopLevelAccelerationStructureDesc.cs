namespace SharpNRI.Native;

public unsafe partial struct BuildTopLevelAccelerationStructureDesc
{
    [NativeTypeName("nri::AccelerationStructure *")]
    public AccelerationStructure* dst;

    [NativeTypeName("const AccelerationStructure *")]
    public AccelerationStructure* src;

    [NativeTypeName("uint32_t")]
    public uint instanceNum;

    [NativeTypeName("const Buffer *")]
    public Buffer* instanceBuffer;

    [NativeTypeName("uint64_t")]
    public ulong instanceOffset;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* scratchBuffer;

    [NativeTypeName("uint64_t")]
    public ulong scratchOffset;
}
