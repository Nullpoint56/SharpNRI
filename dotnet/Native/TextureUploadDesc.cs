namespace PrismNRI.Native;

public unsafe partial struct TextureUploadDesc
{
    [NativeTypeName("const TextureSubresourceUploadDesc *")]
    public TextureSubresourceUploadDesc* subresources;

    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage after;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;
}
