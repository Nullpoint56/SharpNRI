namespace SharpNRI.Native;

public unsafe partial struct TextureD3D12Desc
{
    public ID3D12Resource* d3d12Resource;

    /// <summary>must be provided "as a compatible typed format" if the resource is typeless</summary>
    [NativeTypeName("DXGIFormat")]
    public int format;
}
