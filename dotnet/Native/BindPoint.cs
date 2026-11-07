namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkPipelineBindPoint.html</summary>
[NativeTypeName("uint8_t")]
public enum BindPoint : byte
{
    /// <summary>inherit from the last "CmdSetPipelineLayout" call</summary>
    INHERIT,
    GRAPHICS,
    COMPUTE,
    RAY_TRACING,
    MAX_NUM,
}
