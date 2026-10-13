using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum PipelineLayoutBits : byte
{
    NONE = 0,
    IGNORE_GLOBAL_SPIRV_OFFSETS = (1 << 0),
    ENABLE_DRAW_PARAMETERS_EMULATION = (1 << 1),
    ENABLE_DRAW_INDEX_EMULATION = (1 << 2),
    SAMPLER_HEAP_DIRECTLY_INDEXED = (1 << 3),
    RESOURCE_HEAP_DIRECTLY_INDEXED = (1 << 4),
}
