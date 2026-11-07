namespace SharpNRI.Native;

/// <summary>https://registry.khronos.org/vulkan/specs/latest/man/html/VkPresentScalingFlagBitsKHR.html</summary>
[NativeTypeName("uint8_t")]
public enum Scaling : byte
{
    /// <summary>no scaling, 1:1 pixel mapping</summary>
    ONE_TO_ONE,
    /// <summary>minified or magnified stretching</summary>
    STRETCH,
    MAX_NUM,
}
