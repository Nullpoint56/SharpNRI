namespace PrismNRI.Native;

public unsafe partial struct BufferVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkBuffer;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("uint32_t")]
    public uint structureStride;

    [NativeTypeName("uint8_t *")]
    public byte* mappedMemory;

    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkDeviceMemory;

    [NativeTypeName("uint64_t")]
    public ulong deviceAddress;
}
