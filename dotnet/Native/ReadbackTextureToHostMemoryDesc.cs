namespace SharpNRI.Native;

public unsafe partial struct ReadbackTextureToHostMemoryDesc
{
    /// <summary>must be in "{AccessBits::HOST_READ, Layout::GENERAL, StageBits::HOST}"</summary>
    [NativeTypeName("nri::Texture *")]
    public Texture* srcTexture;

    public void* dstData;

    [NativeTypeName("nri::TextureRegionDesc")]
    public TextureRegionDesc srcRegion;

    /// <summary>if rows are not tightly packed</summary>
    [NativeTypeName("uint32_t")]
    public uint dstRowPitch;

    /// <summary>if slices are not tightly packed</summary>
    [NativeTypeName("uint32_t")]
    public uint dstSlicePitch;
}
