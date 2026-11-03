namespace SharpNRI.Native;

public unsafe partial struct MemoryD3D12Desc
{
    public ID3D12Heap* d3d12Heap;

    [NativeTypeName("uint64_t")]
    public ulong offset;
}
