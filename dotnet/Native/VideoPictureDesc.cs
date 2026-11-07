namespace SharpNRI.Native;

public unsafe partial struct VideoPictureDesc
{
    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::VideoPictureUsage")]
    public VideoPictureUsage usage;

    /// <summary>defaults to the texture width</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    /// <summary>defaults to the texture height</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort height;

    [NativeTypeName("nri::Dim_t")]
    public ushort layer;
}
