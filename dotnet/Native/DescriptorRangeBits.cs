namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkDescriptorBindingFlagBits.html</summary>
[NativeTypeName("uint8_t")]
[Flags]
public enum DescriptorRangeBits : byte
{
    NONE = 0,
    /// <summary>Requires "tiers.resourceBinding &gt;= 1"; descriptor validity is additionally restricted by the tier descriptors in range may not contain valid descriptors at the time the descriptors are consumed (but referenced descriptors must be valid)</summary>
    PARTIALLY_BOUND = (1 << 0),
    /// <summary>descriptors in range are organized into an array</summary>
    ARRAY = (1 << 1),
    /// <summary>Requires "tiers.bindless &gt;= 1" and "tiers.resourceBinding &gt;= 2" VK: only one range per set, resolving to the highest binding number after applying "VKBindingOffsets" descriptors in range are organized into a variable-sized array, which size is specified via "variableDescriptorNum" argument of "AllocateDescriptorSets" function</summary>
    VARIABLE_SIZED_ARRAY = (1 << 2),
    /// <summary>https://docs.vulkan.org/samples/latest/samples/extensions/descriptor_indexing/README.html#_update_after_bind_streaming_descriptors_concurrently WGPU: true "update after set" is unsupported because bind groups are immutable; "update + rebind" can work, but previously recorded commands can't be patched descriptors in range can be updated after "CmdSetDescriptorSet" but before "QueueSubmit", also works as "DATA_VOLATILE"</summary>
    ALLOW_UPDATE_AFTER_SET = (1 << 3),
}
