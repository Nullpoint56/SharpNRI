namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkDescriptorType.html</summary>
[NativeTypeName("uint8_t")]
public enum DescriptorType : byte
{
    /// <summary>Typed HLSL reg Compatible resources Sampler heap - s sampler</summary>
    SAMPLER,
    /// <summary>Resource heap - a mutable descriptor is a proxy "union" descriptor for all resource descriptor types, i.e. non-sampler - a mutable descriptor can't be created, it can only be allocated from a pool (i.e. used in a "DescriptorRangeDesc") - a mutable descriptor must "mutate" to any resource descriptor via "UpdateDescriptorRanges" or "CopyDescriptorRanges" - a mutable descriptor range may include any non-sampler descriptors, which may be directly indexed in shaders - - any non-sampler</summary>
    MUTABLE,
    /// <summary>Optimized resources t TextureView: TEXTURE, TEXTURE_ARRAY, TEXTURE_CUBE, TEXTURE_CUBE_ARRAY</summary>
    TEXTURE,
    /// <summary>u TextureView: STORAGE_TEXTURE, STORAGE_TEXTURE_ARRAY</summary>
    STORAGE_TEXTURE,
    /// <summary>- TextureView: SUBPASS_INPUT</summary>
    INPUT_ATTACHMENT,
    /// <summary>t BufferView: BUFFER</summary>
    BUFFER,
    /// <summary>u BufferView: STORAGE_BUFFER</summary>
    STORAGE_BUFFER,
    /// <summary>- b BufferView: CONSTANT_BUFFER</summary>
    CONSTANT_BUFFER,
    /// <summary>- t BufferView: STRUCTURED_BUFFER, BYTE_ADDRESS_BUFFER</summary>
    STRUCTURED_BUFFER,
    /// <summary>- u BufferView: STORAGE_STRUCTURED_BUFFER, STORAGE_BYTE_ADDRESS_BUFFER</summary>
    STORAGE_STRUCTURED_BUFFER,
    /// <summary>- t acceleration structure, requires "features.rayTracing"</summary>
    ACCELERATION_STRUCTURE,
    MAX_NUM,
}
