namespace PrismNRI.Native;

public partial struct AccelerationStructureVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkAccelerationStructure;

    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkBuffer;

    [NativeTypeName("uint64_t")]
    public ulong bufferSize;

    [NativeTypeName("uint64_t")]
    public ulong buildScratchSize;

    [NativeTypeName("uint64_t")]
    public ulong updateScratchSize;

    [NativeTypeName("nri::AccelerationStructureBits")]
    public AccelerationStructureBits flags;
}
