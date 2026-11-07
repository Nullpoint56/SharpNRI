namespace SharpNRI.Native;

/// <summary>https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_render_pass_beginning_access_type https://docs.vulkan.org/refpages/latest/refpages/source/VkAttachmentLoadOp.html</summary>
[NativeTypeName("uint8_t")]
public enum LoadOp : byte
{
    /// <summary>loads the existing attachment contents</summary>
    LOAD,
    /// <summary>clears the attachment using "AttachmentDesc::clearValue"</summary>
    CLEAR,
    MAX_NUM,
}
