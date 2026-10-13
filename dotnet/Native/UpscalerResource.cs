namespace PrismNRI.Native;

public unsafe partial struct UpscalerResource
{
    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;
}
