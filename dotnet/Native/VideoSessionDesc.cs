namespace PrismNRI.Native;

public partial struct VideoSessionDesc
{
    [NativeTypeName("nri::VideoSessionType")]
    public VideoSessionType type;

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

    [NativeTypeName("bool")]
    public byte av1FilmGrain;
}
