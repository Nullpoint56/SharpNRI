namespace SharpNRI.Native;

/// <summary>Descriptor pool https://learn.microsoft.com/en-us/windows/win32/direct3d12/descriptor-heaps https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_descriptor_heap_desc https://docs.vulkan.org/refpages/latest/refpages/source/VkDescriptorPoolCreateInfo.html</summary>
public partial struct DescriptorPoolDesc
{
    /// <summary>Maximum number of descriptor sets that can be allocated from this pool</summary>
    [NativeTypeName("uint32_t")]
    public uint descriptorSetMaxNum;

    /// <summary>Resource heap - may be directly indexed in shaders via "RESOURCE_HEAP_DIRECTLY_INDEXED" pipeline layout flag - https://docs.vulkan.org/features/latest/features/proposals/VK_EXT_mutable_descriptor_type.html number of "MUTABLE" descriptors, requires "features.mutableDescriptorType"</summary>
    [NativeTypeName("uint32_t")]
    public uint mutableMaxNum;

    /// <summary>Sampler heap - may be directly indexed in shaders via "SAMPLER_HEAP_DIRECTLY_INDEXED" pipeline layout flag - root samplers do not count (not allocated from a descriptor pool) number of "SAMPLER" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint samplerMaxNum;

    /// <summary>Optimized resources (may have various sizes depending on Vulkan implementation) number of "CONSTANT_BUFFER" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint constantBufferMaxNum;

    /// <summary>number of "TEXTURE" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint textureMaxNum;

    /// <summary>number of "STORAGE_TEXTURE" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint storageTextureMaxNum;

    /// <summary>number of "BUFFER" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint bufferMaxNum;

    /// <summary>number of "STORAGE_BUFFER" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint storageBufferMaxNum;

    /// <summary>number of "STRUCTURED_BUFFER" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint structuredBufferMaxNum;

    /// <summary>number of "STORAGE_STRUCTURED_BUFFER" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint storageStructuredBufferMaxNum;

    /// <summary>number of "ACCELERATION_STRUCTURE" descriptors, requires "features.rayTracing"</summary>
    [NativeTypeName("uint32_t")]
    public uint accelerationStructureMaxNum;

    /// <summary>number of "INPUT_ATTACHMENT" descriptors</summary>
    [NativeTypeName("uint32_t")]
    public uint inputAttachmentMaxNum;

    [NativeTypeName("nri::DescriptorPoolBits")]
    public DescriptorPoolBits flags;
}
