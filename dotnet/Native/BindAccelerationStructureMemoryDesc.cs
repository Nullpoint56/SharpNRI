namespace SharpNRI.Native;

public unsafe partial struct BindAccelerationStructureMemoryDesc
{
    [NativeTypeName("nri::AccelerationStructure *")]
    public AccelerationStructure* accelerationStructure;

    [NativeTypeName("nri::Memory *")]
    public Memory* memory;

    [NativeTypeName("uint64_t")]
    public ulong offset;
}
