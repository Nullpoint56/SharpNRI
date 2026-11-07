namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum GraphicsPipelineBits : byte
{
    NONE = 0,
    /// <summary>"CreateGraphicsPipeline" returns "FAILURE" if the pipeline is not found in the supplied cache (requires "features.pipelineCacheControl")</summary>
    FAIL_ON_CACHE_MISS = (1 << 0),
}
