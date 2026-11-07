namespace SharpNRI.Native;

public unsafe partial struct UploadHostMemoryToTextureDesc
{
    [NativeTypeName("const void *")]
    public void* srcData;

    /// <summary>must be in "{AccessBits::HOST_WRITE, Layout::GENERAL, StageBits::HOST}"</summary>
    [NativeTypeName("nri::Texture *")]
    public Texture* dstTexture;

    [NativeTypeName("nri::TextureRegionDesc")]
    public TextureRegionDesc dstRegion;

    /// <summary>if rows are not tightly packed</summary>
    [NativeTypeName("uint32_t")]
    public uint srcRowPitch;

    /// <summary>if slices are not tightly packed</summary>
    [NativeTypeName("uint32_t")]
    public uint srcSlicePitch;
}
