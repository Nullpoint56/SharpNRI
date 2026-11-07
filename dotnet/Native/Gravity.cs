namespace SharpNRI.Native;

/// <summary>https://registry.khronos.org/vulkan/specs/latest/man/html/VkPresentGravityFlagBitsKHR.html</summary>
[NativeTypeName("uint8_t")]
public enum Gravity : byte
{
    /// <summary>pixels will gravitate towards the top or left side of the surface (Windows style)</summary>
    MIN,
    /// <summary>VK: pixels will gravitate towards the bottom or right side of the surface</summary>
    MAX,
    /// <summary>VK: pixels will be centered in the surface</summary>
    CENTERED,
    MAX_NUM,
}
