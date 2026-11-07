namespace SharpNRI.Native;

public unsafe partial struct StreamTextureDataDesc
{
    /// <summary>Data to upload</summary>
    [NativeTypeName("const void *")]
    public void* data;

    /// <summary>0 means tightly packed rows</summary>
    [NativeTypeName("uint32_t")]
    public uint dataRowPitch;

    /// <summary>0 means effective row pitch * number of texel-block rows</summary>
    [NativeTypeName("uint32_t")]
    public uint dataSlicePitch;

    /// <summary>Destination</summary>
    [NativeTypeName("nri::StreamerCopyBatch")]
    public ulong copyBatch;

    [NativeTypeName("nri::Texture *")]
    public Texture* dstTexture;

    [NativeTypeName("nri::TextureRegionDesc")]
    public TextureRegionDesc dstRegion;
}
