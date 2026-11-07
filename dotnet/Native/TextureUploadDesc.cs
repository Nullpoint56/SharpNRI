namespace SharpNRI.Native;

public unsafe partial struct TextureUploadDesc
{
    /// <summary>if provided, must include ALL subresources = layerNum * mipNum</summary>
    [NativeTypeName("const TextureSubresourceUploadDesc *")]
    public TextureSubresourceUploadDesc* subresources;

    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage after;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;
}
