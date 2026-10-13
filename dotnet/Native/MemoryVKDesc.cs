namespace PrismNRI.Native;

public unsafe partial struct MemoryVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkDeviceMemory;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    public void* mappedMemory;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint32_t")]
    public uint memoryTypeIndex;
}
