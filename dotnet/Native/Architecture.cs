namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceType.html</summary>
[NativeTypeName("uint8_t")]
public enum Architecture : byte
{
    UNKNOWN,
    /// <summary>CPU</summary>
    SOFTWARE,
    /// <summary>remote desktop?</summary>
    VIRTUAL,
    /// <summary>UMA</summary>
    INTEGRATED,
    /// <summary>yes, please!</summary>
    DISCRETE,
    MAX_NUM,
}
