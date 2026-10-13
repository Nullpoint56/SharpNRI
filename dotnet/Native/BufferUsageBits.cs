using System;
namespace PrismNRI.Native;

[NativeTypeName("uint16_t")]
[Flags]
public enum BufferUsageBits : ushort
{
    NONE = 0,
    SHADER_RESOURCE = (1 << 0),
    SHADER_RESOURCE_STORAGE = (1 << 1),
    VERTEX = (1 << 2),
    INDEX = (1 << 3),
    CONSTANT = (1 << 4),
    ARGUMENT = (1 << 5),
    SCRATCH = (1 << 6),
    SHADER_BINDING_TABLE = (1 << 7),
    ACCELERATION_STRUCTURE_BUILD_INPUT = (1 << 8),
    ACCELERATION_STRUCTURE_STORAGE = (1 << 9),
    MICROMAP_BUILD_INPUT = (1 << 10),
    MICROMAP_STORAGE = (1 << 11),
    VIDEO_DECODE = (1 << 12),
    VIDEO_ENCODE = (1 << 13),
}
