namespace SharpNRI.Native;

public unsafe partial struct AccelerationStructureD3D12Desc
{
    public ID3D12Resource* d3d12Resource;

    [NativeTypeName("nri::AccelerationStructureBits")]
    public AccelerationStructureBits flags;

    /// <summary>D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO</summary>
    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint64_t")]
    public ulong buildScratchSize;

    [NativeTypeName("uint64_t")]
    public ulong updateScratchSize;
}
