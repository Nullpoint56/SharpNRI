namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum PipelineLayoutBits : byte
{
    NONE = 0,
    /// <summary>VK: ignore "DeviceCreationDesc::vkBindingOffsets"</summary>
    IGNORE_GLOBAL_SPIRV_OFFSETS = (1 << 0),
    /// <summary>D3D12, METAL: enable draw parameters emulation, requires "shaderFeatures.drawParameters"</summary>
    ENABLE_DRAW_PARAMETERS_EMULATION = (1 << 1),
    /// <summary>D3D12, METAL: enable draw index emulation, requires "shaderFeatures.drawIndex"</summary>
    ENABLE_DRAW_INDEX_EMULATION = (1 << 2),
    /// <summary>Direct indexing has two modes: - "descriptor pool": "MUTABLE" descriptors + "DIRECTLY_INDEXED" flags + up to two ranges in a set describing resource and sampler "virtual heaps" in a descriptor pool https://github.com/Microsoft/DirectXShaderCompiler/blob/main/docs/SPIR-V.rst#resourcedescriptorheaps--samplerdescriptorheaps Default VK bindings can be changed via "-fvk-bind-sampler-heap" and "-fvk-bind-resource-heap" DXC options - "descriptor heap": NRIDescriptorHeap functionality (requires "features.descriptorHeap") + no descriptor sets + at least one "DIRECTLY_INDEXED" flag requires "shaderModel &gt;= 66"</summary>
    SAMPLER_HEAP_DIRECTLY_INDEXED = (1 << 3),
    /// <summary>requires "shaderModel &gt;= 66"</summary>
    RESOURCE_HEAP_DIRECTLY_INDEXED = (1 << 4),
}
