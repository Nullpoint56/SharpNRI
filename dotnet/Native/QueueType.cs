namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkQueueFlagBits.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_command_list_type</summary>
[NativeTypeName("uint8_t")]
public enum QueueType : byte
{
    GRAPHICS,
    COMPUTE,
    COPY,
    VIDEO_DECODE,
    VIDEO_ENCODE,
    MAX_NUM,
}
