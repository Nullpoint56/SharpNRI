namespace SharpNRI.Native;

public unsafe partial struct BufferD3D12Desc
{
    public ID3D12Resource* d3d12Resource;

    [NativeTypeName("const BufferDesc *")]
    public BufferDesc* desc;

    [NativeTypeName("uint32_t")]
    public uint structureStride;
}
