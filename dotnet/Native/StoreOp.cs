namespace SharpNRI.Native;

/// <summary>https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_render_pass_ending_access_type https://docs.vulkan.org/refpages/latest/refpages/source/VkAttachmentStoreOp.html</summary>
[NativeTypeName("uint8_t")]
public enum StoreOp : byte
{
    /// <summary>stores the attachment contents</summary>
    STORE,
    /// <summary>makes the attachment contents undefined</summary>
    DISCARD,
    /// <summary>performs no store access if the attachment is not written, otherwise acts like "DISCARD"</summary>
    NONE,
    MAX_NUM,
}
