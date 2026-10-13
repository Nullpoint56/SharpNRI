namespace PrismNRI.Native;

public partial struct TextureVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkImage;

    [NativeTypeName("VKEnum")]
    public int vkFormat;

    [NativeTypeName("VKEnum")]
    public int vkImageType;

    [NativeTypeName("VKFlags")]
    public uint vkImageUsageFlags;

    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    [NativeTypeName("nri::Dim_t")]
    public ushort height;

    [NativeTypeName("nri::Dim_t")]
    public ushort depth;

    [NativeTypeName("nri::Dim_t")]
    public ushort mipNum;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerNum;

    [NativeTypeName("nri::Sample_t")]
    public byte sampleNum;
}
