namespace SharpNRI.Native;

public unsafe partial struct BufferD3D12Desc
{
    public ID3D12Resource* d3d12Resource;

    /// <summary>not all information can be retrieved from the resource if not provided</summary>
    [NativeTypeName("const BufferDesc *")]
    public BufferDesc* desc;

    /// <summary>must be provided if used as a structured or raw buffer</summary>
    [NativeTypeName("uint32_t")]
    public uint structureStride;
}
