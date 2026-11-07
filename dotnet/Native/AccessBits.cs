namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkAccessFlagBits2.html https://microsoft.github.io/DirectX-Specs/d3d/D3D12EnhancedBarriers.html#d3d12_barrier_access</summary>
[NativeTypeName("uint32_t")]
[Flags]
public enum AccessBits : uint
{
    /// <summary>Mapped to "COMMON" (aka "GENERAL" access), if AgilitySDK is not available, leading to potential discrepancies with VK</summary>
    NONE = 0,
    /// <summary>Buffer // Access Compatible "StageBits" (including ALL) R INDEX_INPUT</summary>
    INDEX_BUFFER = (1U << (0)),
    /// <summary>R VERTEX_SHADER</summary>
    VERTEX_BUFFER = (1U << (1)),
    /// <summary>R ALL_SHADERS</summary>
    CONSTANT_BUFFER = (1U << (2)),
    /// <summary>R INDIRECT</summary>
    ARGUMENT_BUFFER = (1U << (3)),
    /// <summary>RW ACCELERATION_STRUCTURE, MICROMAP</summary>
    SCRATCH_BUFFER = (1U << (4)),
    /// <summary>Attachment R COLOR_ATTACHMENT (implicitly by ROP)</summary>
    COLOR_ATTACHMENT_READ = (1U << (5)),
    /// <summary>W COLOR_ATTACHMENT</summary>
    COLOR_ATTACHMENT_WRITE = (1U << (6)),
    /// <summary>R DEPTH_STENCIL_ATTACHMENT</summary>
    DEPTH_STENCIL_ATTACHMENT_READ = (1U << (7)),
    /// <summary>W DEPTH_STENCIL_ATTACHMENT</summary>
    DEPTH_STENCIL_ATTACHMENT_WRITE = (1U << (8)),
    /// <summary>R SHADING_RATE_ATTACHMENT</summary>
    SHADING_RATE_ATTACHMENT = (1U << (9)),
    /// <summary>R FRAGMENT_SHADER</summary>
    INPUT_ATTACHMENT = (1U << (10)),
    /// <summary>Acceleration structure R COMPUTE_SHADER, RAY_TRACING_SHADERS, ACCELERATION_STRUCTURE</summary>
    ACCELERATION_STRUCTURE_READ = (1U << (11)),
    /// <summary>W ACCELERATION_STRUCTURE</summary>
    ACCELERATION_STRUCTURE_WRITE = (1U << (12)),
    /// <summary>Micromap R MICROMAP, ACCELERATION_STRUCTURE</summary>
    MICROMAP_READ = (1U << (13)),
    /// <summary>W MICROMAP</summary>
    MICROMAP_WRITE = (1U << (14)),
    /// <summary>Shader R ALL_SHADERS</summary>
    SHADER_RESOURCE = (1U << (15)),
    /// <summary>RW ALL_SHADERS, CLEAR_STORAGE</summary>
    SHADER_RESOURCE_STORAGE = (1U << (16)),
    /// <summary>R RAY_TRACING_SHADERS</summary>
    SHADER_BINDING_TABLE = (1U << (17)),
    /// <summary>Copy R COPY</summary>
    COPY_SOURCE = (1U << (18)),
    /// <summary>W COPY</summary>
    COPY_DESTINATION = (1U << (19)),
    /// <summary>Resolve R RESOLVE</summary>
    RESOLVE_SOURCE = (1U << (20)),
    /// <summary>W RESOLVE</summary>
    RESOLVE_DESTINATION = (1U << (21)),
    /// <summary>R RESOLVE_ATTACHMENT</summary>
    RESOLVE_ATTACHMENT_SOURCE = (1U << (22)),
    /// <summary>RW RESOLVE_ATTACHMENT</summary>
    RESOLVE_ATTACHMENT_DESTINATION = (1U << (23)),
    /// <summary>Clear storage W CLEAR_STORAGE</summary>
    CLEAR_STORAGE = (1U << (24)),
    /// <summary>Host R HOST</summary>
    HOST_READ = (1U << (25)),
    /// <summary>W HOST</summary>
    HOST_WRITE = (1U << (26)),
    /// <summary>Video R VIDEO_DECODE</summary>
    VIDEO_DECODE_READ = (1U << (27)),
    /// <summary>W VIDEO_DECODE</summary>
    VIDEO_DECODE_WRITE = (1U << (28)),
    /// <summary>R VIDEO_ENCODE</summary>
    VIDEO_ENCODE_READ = (1U << (29)),
    /// <summary>W VIDEO_ENCODE</summary>
    VIDEO_ENCODE_WRITE = (1U << (30)),
    /// <summary>Umbrella access</summary>
    COLOR_ATTACHMENT = COLOR_ATTACHMENT_READ | COLOR_ATTACHMENT_WRITE,
    DEPTH_STENCIL_ATTACHMENT = DEPTH_STENCIL_ATTACHMENT_READ | DEPTH_STENCIL_ATTACHMENT_WRITE,
    ACCELERATION_STRUCTURE = ACCELERATION_STRUCTURE_READ | ACCELERATION_STRUCTURE_WRITE,
    MICROMAP = MICROMAP_READ | MICROMAP_WRITE,
    VIDEO_DECODE = VIDEO_DECODE_READ | VIDEO_DECODE_WRITE,
    VIDEO_ENCODE = VIDEO_ENCODE_READ | VIDEO_ENCODE_WRITE,
}
