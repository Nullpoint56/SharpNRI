namespace SharpNRI.Native;

public partial struct VideoSessionDesc
{
    [NativeTypeName("nri::VideoSessionType")]
    public VideoSessionType type;

    /// <summary>fixed profiles: H.264 High, H.265 Main/Main10 (by format), AV1 Main; H.264 sessions are progressive-only</summary>
    [NativeTypeName("nri::VideoCodec")]
    public VideoCodec codec;

    [NativeTypeName("nri::Format")]
    public Format format;

    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    [NativeTypeName("uint32_t")]
    public uint maxReferenceNum;

    /// <summary>AV1 decode: permits "APPLY_GRAIN"; textures require "TextureDesc::videoAV1FilmGrain"; UNSUPPORTED if unavailable</summary>
    [NativeTypeName("bool")]
    public byte av1FilmGrain;
}
