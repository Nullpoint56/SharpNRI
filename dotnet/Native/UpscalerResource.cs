namespace SharpNRI.Native;

public unsafe partial struct UpscalerResource
{
    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    /// <summary>"TEXTURE" or "STORAGE_TEXTURE" descriptor, see requirements below</summary>
    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;
}
