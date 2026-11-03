namespace SharpNRI.Native;

public partial struct DescriptorPoolDesc
{
    [NativeTypeName("uint32_t")]
    public uint descriptorSetMaxNum;

    [NativeTypeName("uint32_t")]
    public uint mutableMaxNum;

    [NativeTypeName("uint32_t")]
    public uint samplerMaxNum;

    [NativeTypeName("uint32_t")]
    public uint constantBufferMaxNum;

    [NativeTypeName("uint32_t")]
    public uint textureMaxNum;

    [NativeTypeName("uint32_t")]
    public uint storageTextureMaxNum;

    [NativeTypeName("uint32_t")]
    public uint bufferMaxNum;

    [NativeTypeName("uint32_t")]
    public uint storageBufferMaxNum;

    [NativeTypeName("uint32_t")]
    public uint structuredBufferMaxNum;

    [NativeTypeName("uint32_t")]
    public uint storageStructuredBufferMaxNum;

    [NativeTypeName("uint32_t")]
    public uint accelerationStructureMaxNum;

    [NativeTypeName("uint32_t")]
    public uint inputAttachmentMaxNum;

    [NativeTypeName("nri::DescriptorPoolBits")]
    public DescriptorPoolBits flags;
}
