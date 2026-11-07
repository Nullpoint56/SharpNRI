namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkVertexInputRate.html</summary>
[NativeTypeName("uint8_t")]
public enum VertexStreamStepRate : byte
{
    PER_VERTEX,
    PER_INSTANCE,
    MAX_NUM,
}
