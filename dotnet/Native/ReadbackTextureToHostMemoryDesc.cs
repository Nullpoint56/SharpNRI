namespace PrismNRI.Native;

public unsafe partial struct ReadbackTextureToHostMemoryDesc
{
    [NativeTypeName("nri::Texture *")]
    public Texture* srcTexture;

    public void* dstData;

    [NativeTypeName("nri::TextureRegionDesc")]
    public TextureRegionDesc srcRegion;

    [NativeTypeName("uint32_t")]
    public uint dstRowPitch;

    [NativeTypeName("uint32_t")]
    public uint dstSlicePitch;
}
