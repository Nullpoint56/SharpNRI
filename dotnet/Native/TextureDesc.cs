namespace SharpNRI.Native;

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

    /// <summary>VK: required for video textures</summary>
    [NativeTypeName("nri::VideoCodec")]
    public VideoCodec videoCodec;

    /// <summary>VK: AV1 decode textures of sessions with "VideoSessionDesc::av1FilmGrain"</summary>
    [NativeTypeName("bool")]
    public byte videoAV1FilmGrain;

    /// <summary>D3D12: not needed on desktop, since any HW can track many clear values</summary>
    [NativeTypeName("nri::ClearValue")]
    public ClearValue optimizedClearValue;

    /// <summary>"GetTextureSharedHandle": requires "CreateCommittedTexture" in "DEVICE" memory and "features.sharedResources"</summary>
    [NativeTypeName("bool")]
    public byte shared;
}
