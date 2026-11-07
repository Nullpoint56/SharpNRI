namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum RayTracingPipelineBits : byte
{
    NONE = 0,
    /// <summary>provides knowledge that "triangles" doesn't need to be considered</summary>
    SKIP_TRIANGLES = (1 << 0),
    /// <summary>provides knowledge that "aabbs" doesn't need to be considered</summary>
    SKIP_AABBS = (1 << 1),
    /// <summary>specifies that the ray tracing pipeline can be used with acceleration structures which reference micromaps</summary>
    ALLOW_MICROMAPS = (1 << 2),
    /// <summary>"CreateRayTracingPipeline" returns "FAILURE" if the pipeline is not found in the supplied cache (requires "features.pipelineCacheControl")</summary>
    FAIL_ON_CACHE_MISS = (1 << 3),
}
