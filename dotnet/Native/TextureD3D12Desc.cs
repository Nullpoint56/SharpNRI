namespace SharpNRI.Native;

public unsafe partial struct TextureD3D12Desc
{
    public ID3D12Resource* d3d12Resource;

    [NativeTypeName("DXGIFormat")]
    public int format;
}
