namespace SharpNRI.Native;

/// <summary>Texture sharing between local queues (see "TextureBarrierDesc")</summary>
[NativeTypeName("uint8_t")]
public enum SharingMode : byte
{
    /// <summary>default; no ownership transfers between local queues</summary>
    CONCURRENT,
    /// <summary>VK: may preserve attachment compression; may need local ownership transfers</summary>
    EXCLUSIVE,
    /// <summary>https://microsoft.github.io/DirectX-Specs/d3d/D3D12EnhancedBarriers.html#single-queue-simultaneous-access https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_resource_flags D3D12: concurrent reads and one write to non-overlapping regions (requires "Layout::GENERAL")</summary>
    SIMULTANEOUS,
    MAX_NUM,
}
