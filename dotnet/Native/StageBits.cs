namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkPipelineStageFlagBits2.html https://microsoft.github.io/DirectX-Specs/d3d/D3D12EnhancedBarriers.html#d3d12_barrier_sync</summary>
[NativeTypeName("uint32_t")]
[Flags]
public enum StageBits : uint
{
    /// <summary>Special Lazy default for barriers and "shaderStages"</summary>
    ALL = 0,
    NONE = 0x7FFFFFFF,
    /// <summary>Graphics // Invoked by "CmdDraw*" Shader stage Index buffer consumption</summary>
    INDEX_INPUT = (1U << (0)),
    /// <summary>Vertex shader X (required within GRAPHICS bind point)</summary>
    VERTEX_SHADER = (1U << (1)),
    /// <summary>Tessellation control (hull) shader X</summary>
    TESS_CONTROL_SHADER = (1U << (2)),
    /// <summary>Tessellation evaluation (domain) shader X</summary>
    TESS_EVALUATION_SHADER = (1U << (3)),
    /// <summary>Geometry shader X</summary>
    GEOMETRY_SHADER = (1U << (4)),
    /// <summary>Task (amplification) shader X</summary>
    TASK_SHADER = (1U << (5)),
    /// <summary>Mesh shader X (or required within GRAPHICS bind point)</summary>
    MESH_SHADER = (1U << (6)),
    /// <summary>Fragment (pixel) shader X</summary>
    FRAGMENT_SHADER = (1U << (7)),
    /// <summary>Depth-stencil R/W operations</summary>
    DEPTH_STENCIL_ATTACHMENT = (1U << (8)),
    /// <summary>Color R/W operations</summary>
    COLOR_ATTACHMENT = (1U << (9)),
    /// <summary>Shading rate attachment R</summary>
    SHADING_RATE_ATTACHMENT = (1U << (10)),
    /// <summary>Compute // Invoked by "CmdDispatch*" (not Rays) Compute shader X (required within COMPUTE bind point)</summary>
    COMPUTE_SHADER = (1U << (11)),
    /// <summary>Ray tracing // Invoked by "CmdDispatchRays*" Ray generation shader X (required within RAY_TRACING bind point)</summary>
    RAYGEN_SHADER = (1U << (12)),
    /// <summary>Miss shader X</summary>
    MISS_SHADER = (1U << (13)),
    /// <summary>Intersection shader X</summary>
    INTERSECTION_SHADER = (1U << (14)),
    /// <summary>Closest hit shader X</summary>
    CLOSEST_HIT_SHADER = (1U << (15)),
    /// <summary>Any hit shader X</summary>
    ANY_HIT_SHADER = (1U << (16)),
    /// <summary>Callable shader X</summary>
    CALLABLE_SHADER = (1U << (17)),
    /// <summary>Invoked by "Cmd*AccelerationStructure*" commands</summary>
    ACCELERATION_STRUCTURE = (1U << (18)),
    /// <summary>Invoked by "Cmd*Micromap*" commands</summary>
    MICROMAP = (1U << (19)),
    /// <summary>Resolve Invoked by "CmdResolveTexture"</summary>
    RESOLVE = (1U << (20)),
    /// <summary>Invoked by "AttachmentDesc::resolveDst"</summary>
    RESOLVE_ATTACHMENT = (1U << (21)),
    /// <summary>Other Invoked by "CmdCopy*", "CmdUpload*", "CmdReadback*" and "CmdZeroBuffer"</summary>
    COPY = (1U << (22)),
    /// <summary>Invoked by "CmdClearStorage"</summary>
    CLEAR_STORAGE = (1U << (23)),
    /// <summary>Modifiers Invoked by "Indirect" commands (used in addition to other bits)</summary>
    INDIRECT = (1U << (24)),
    /// <summary>Host Invoked by "UploadHostMemoryToTexture" and "ReadbackTextureToHostMemory"</summary>
    HOST = (1U << (25)),
    /// <summary>Video Invoked by "CmdDecodeVideo"</summary>
    VIDEO_DECODE = (1U << (26)),
    /// <summary>Invoked by "CmdEncodeVideo"</summary>
    VIDEO_ENCODE = (1U << (27)),
    /// <summary>Umbrella stages</summary>
    TESSELLATION_SHADERS = TESS_CONTROL_SHADER | TESS_EVALUATION_SHADER,
    MESH_SHADERS = TASK_SHADER | MESH_SHADER,
    GRAPHICS_SHADERS = VERTEX_SHADER | TESSELLATION_SHADERS | GEOMETRY_SHADER | MESH_SHADERS | FRAGMENT_SHADER,
    RAY_TRACING_SHADERS = RAYGEN_SHADER | MISS_SHADER | INTERSECTION_SHADER | CLOSEST_HIT_SHADER | ANY_HIT_SHADER | CALLABLE_SHADER,
    /// <summary>For descriptor and root visibility, "ALL" and "ALL_SHADERS" resolve to "PipelineLayoutDesc::shaderStages"</summary>
    ALL_SHADERS = GRAPHICS_SHADERS | COMPUTE_SHADER | RAY_TRACING_SHADERS,
    ALL_ATTACHMENT = COLOR_ATTACHMENT | DEPTH_STENCIL_ATTACHMENT | SHADING_RATE_ATTACHMENT,
    GRAPHICS = INDEX_INPUT | GRAPHICS_SHADERS | ALL_ATTACHMENT,
    /// <summary>Resolve stages are explicit in "ALL_RESOLVE" and excluded from "ALL_ATTACHMENT" and "GRAPHICS"</summary>
    ALL_RESOLVE = RESOLVE | RESOLVE_ATTACHMENT,
}
