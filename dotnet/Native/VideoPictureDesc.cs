namespace SharpNRI.Native;

public unsafe partial struct VideoPictureDesc
{
    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::VideoPictureUsage")]
    public VideoPictureUsage usage;

    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    [NativeTypeName("nri::Dim_t")]
    public ushort height;

    [NativeTypeName("nri::Dim_t")]
    public ushort layer;
}
