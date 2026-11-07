namespace SharpNRI.Native;

/// <summary>- Buffers can move between local queues without ownership transfers - D3D11: "structureStride != 0" locks this buffer to a single "STRUCTURED" layout, unless "byteAddress" is set to "true" - D3D11: "byteAddress = true" allows to create multiple "STRUCTURED" views for a single resource by treating a "STRUCTURED" view as "BYTE_ADDRESS" (spec violation) - WGPU: typed buffer views are unsupported (i.e. "structureStride = 0" and "byteAddress = false")</summary>
public partial struct BufferDesc
{
    [NativeTypeName("uint64_t")]
    public ulong size;

    /// <summary>enable "STRUCTURED" views</summary>
    [NativeTypeName("uint32_t")]
    public uint structureStride;

    [NativeTypeName("nri::BufferUsageBits")]
    public BufferUsageBits usage;

    /// <summary>enable "BYTE_ADDRESS" views</summary>
    [NativeTypeName("bool")]
    public byte byteAddress;

    /// <summary>"GetBufferSharedHandle": requires "CreateCommittedBuffer" in "DEVICE" memory and "features.sharedResources"</summary>
    [NativeTypeName("bool")]
    public byte shared;
}
