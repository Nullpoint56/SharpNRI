namespace PrismNRI.Native;

public unsafe partial struct StreamTextureDataDesc
{
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint32_t")]
    public uint dataRowPitch;

    [NativeTypeName("uint32_t")]
    public uint dataSlicePitch;

    [NativeTypeName("nri::StreamerCopyBatch")]
    public ulong copyBatch;

    [NativeTypeName("nri::Texture *")]
    public Texture* dstTexture;

    [NativeTypeName("nri::TextureRegionDesc")]
    public TextureRegionDesc dstRegion;
}
