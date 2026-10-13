namespace PrismNRI.Native;

public unsafe partial struct AccelerationStructureD3D12Desc
{
    public ID3D12Resource* d3d12Resource;

    [NativeTypeName("nri::AccelerationStructureBits")]
    public AccelerationStructureBits flags;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint64_t")]
    public ulong buildScratchSize;

    [NativeTypeName("uint64_t")]
    public ulong updateScratchSize;
}
