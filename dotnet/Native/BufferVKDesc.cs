namespace SharpNRI.Native;

public unsafe partial struct BufferVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkBuffer;

    [NativeTypeName("uint64_t")]
    public ulong size;

    /// <summary>must be provided if used as a structured or raw buffer</summary>
    [NativeTypeName("uint32_t")]
    public uint structureStride;

    /// <summary>must be provided if the underlying memory is mapped</summary>
    [NativeTypeName("uint8_t *")]
    public byte* mappedMemory;

    /// <summary>must be provided *only* if the mapped memory exists and *not* HOST_COHERENT</summary>
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkDeviceMemory;

    /// <summary>must be provided for ray tracing</summary>
    [NativeTypeName("uint64_t")]
    public ulong deviceAddress;
}
