namespace SharpNRI.Native;

/// <summary>https://microsoft.github.io/DirectX-Specs/d3d/CountersAndQueries.html https://docs.vulkan.org/refpages/latest/refpages/source/VkQueryType.html</summary>
[NativeTypeName("uint8_t")]
public enum QueryType : byte
{
    /// <summary>uint64_t, requires "features.timestamp" (for "GRAPHICS" and "COMPUTE" queues), use only "CmdEndQuery" without "CmdBeginQuery"</summary>
    TIMESTAMP,
    /// <summary>uint64_t, requires "features.timestampCopyQueue" (for a "COPY" queue), use only "CmdEndQuery" without "CmdBeginQuery"</summary>
    TIMESTAMP_COPY_QUEUE,
    /// <summary>uint64_t, requires "features.occlusion"</summary>
    OCCLUSION,
    /// <summary>see "PipelineStatisticsDesc", requires "features.pipelineStatistics"</summary>
    PIPELINE_STATISTICS,
    /// <summary>uint64_t, requires "features.rayTracing"</summary>
    ACCELERATION_STRUCTURE_SIZE,
    /// <summary>uint64_t, requires "features.rayTracing"</summary>
    ACCELERATION_STRUCTURE_COMPACTED_SIZE,
    /// <summary>uint64_t, requires "features.micromap"</summary>
    MICROMAP_COMPACTED_SIZE,
    MAX_NUM,
}
