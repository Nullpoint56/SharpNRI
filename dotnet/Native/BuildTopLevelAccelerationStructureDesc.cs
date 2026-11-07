namespace SharpNRI.Native;

public unsafe partial struct BuildTopLevelAccelerationStructureDesc
{
    [NativeTypeName("nri::AccelerationStructure *")]
    public AccelerationStructure* dst;

    /// <summary>implies "update" instead of "build" if provided (requires "ALLOW_UPDATE")</summary>
    [NativeTypeName("const AccelerationStructure *")]
    public AccelerationStructure* src;

    [NativeTypeName("uint32_t")]
    public uint instanceNum;

    /// <summary>contains "TopLevelInstance" entries</summary>
    [NativeTypeName("const Buffer *")]
    public Buffer* instanceBuffer;

    [NativeTypeName("uint64_t")]
    public ulong instanceOffset;

    /// <summary>use "GetAccelerationStructureBuildScratchBufferSize" or "GetAccelerationStructureUpdateScratchBufferSize" to determine the required size</summary>
    [NativeTypeName("nri::Buffer *")]
    public Buffer* scratchBuffer;

    [NativeTypeName("uint64_t")]
    public ulong scratchOffset;
}
