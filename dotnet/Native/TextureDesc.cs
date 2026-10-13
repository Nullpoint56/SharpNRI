namespace PrismNRI.Native;

public partial struct TextureDesc
{
    [NativeTypeName("nri::TextureType")]
    public TextureType type;

    [NativeTypeName("nri::TextureUsageBits")]
    public TextureUsageBits usage;

    [NativeTypeName("nri::Format")]
    public Format format;

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

    [NativeTypeName("nri::SharingMode")]
    public SharingMode sharingMode;

    [NativeTypeName("nri::VideoCodec")]
    public VideoCodec videoCodec;

    [NativeTypeName("nri::ClearValue")]
    public ClearValue optimizedClearValue;
}
