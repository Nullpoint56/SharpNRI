namespace SharpNRI.Native;

public partial struct TextureDataLayoutDesc
{
    /// <summary>a buffer offset must be a multiple of "uploadBufferTextureSliceAlignment" (data placement alignment)</summary>
    [NativeTypeName("uint64_t")]
    public ulong offset;

    /// <summary>must be a multiple of "uploadBufferTextureRowAlignment"</summary>
    [NativeTypeName("uint32_t")]
    public uint rowPitch;

    /// <summary>must be a multiple of "uploadBufferTextureSliceAlignment"</summary>
    [NativeTypeName("uint32_t")]
    public uint slicePitch;
}
