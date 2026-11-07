namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkBufferUsageFlagBits.html</summary>
[NativeTypeName("uint16_t")]
[Flags]
public enum BufferUsageBits : ushort
{
    NONE = 0,
    /// <summary>SHADER_RESOURCE Read-only shader resource view (SRV)</summary>
    SHADER_RESOURCE = (1 << 0),
    /// <summary>SHADER_RESOURCE_STORAGE Read/write shader resource view (UAV)</summary>
    SHADER_RESOURCE_STORAGE = (1 << 1),
    /// <summary>VERTEX_BUFFER Vertex buffer</summary>
    VERTEX = (1 << 2),
    /// <summary>INDEX_BUFFER Index buffer</summary>
    INDEX = (1 << 3),
    /// <summary>CONSTANT_BUFFER Constant buffer (D3D11: can't be combined with other usages)</summary>
    CONSTANT = (1 << 4),
    /// <summary>ARGUMENT_BUFFER Argument buffer in "Indirect" commands</summary>
    ARGUMENT = (1 << 5),
    /// <summary>SCRATCH_BUFFER Scratch buffer in "CmdBuild*" commands</summary>
    SCRATCH = (1 << 6),
    /// <summary>SHADER_BINDING_TABLE Shader binding table (SBT) in "CmdDispatchRays*" commands</summary>
    SHADER_BINDING_TABLE = (1 << 7),
    /// <summary>SHADER_RESOURCE Read-only input in "CmdBuildAccelerationStructures" command</summary>
    ACCELERATION_STRUCTURE_BUILD_INPUT = (1 << 8),
    /// <summary>ACCELERATION_STRUCTURE_READ/WRITE (INTERNAL) acceleration structure storage</summary>
    ACCELERATION_STRUCTURE_STORAGE = (1 << 9),
    /// <summary>SHADER_RESOURCE Read-only input in "CmdBuildMicromaps" command</summary>
    MICROMAP_BUILD_INPUT = (1 << 10),
    /// <summary>MICROMAP_READ/WRITE (INTERNAL) micromap storage</summary>
    MICROMAP_STORAGE = (1 << 11),
    /// <summary>VIDEO_DECODE Video decode bitstream input</summary>
    VIDEO_DECODE = (1 << 12),
    /// <summary>VIDEO_ENCODE Video encode bitstream output</summary>
    VIDEO_ENCODE = (1 << 13),
}
