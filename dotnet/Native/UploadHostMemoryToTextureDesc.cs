namespace SharpNRI.Native;

public unsafe partial struct UploadHostMemoryToTextureDesc
{
    [NativeTypeName("const void *")]
    public void* srcData;

    [NativeTypeName("nri::Texture *")]
    public Texture* dstTexture;

    [NativeTypeName("nri::TextureRegionDesc")]
    public TextureRegionDesc dstRegion;

    [NativeTypeName("uint32_t")]
    public uint srcRowPitch;

    [NativeTypeName("uint32_t")]
    public uint srcSlicePitch;
}
