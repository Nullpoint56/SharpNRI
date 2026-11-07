namespace SharpNRI.Native;

/// <summary>Shared handles export buffers, textures and fences to an external user: - To use imported objects in NRI, import natively first, then use the wrapper extensions - Resource exports are dedicated allocations - CUDA: VK timeline fences require "TimelineSemaphoreWin32/Fd" imports, binary semaphores use "OpaqueWin32/Fd" - After an FD import takes ownership, clear "SharedHandle" instead of destroying it - VK: unsupported resource/semaphore export configurations return "UNSUPPORTED"</summary>
[NativeTypeName("uint8_t")]
public enum SharedHandleType : byte
{
    NONE,
    /// <summary>VK opaque NT handle</summary>
    OPAQUE_WIN32,
    /// <summary>VK opaque file descriptor</summary>
    OPAQUE_FD,
    /// <summary>D3D12 resource NT handle</summary>
    D3D12_RESOURCE,
    /// <summary>D3D12 fence NT handle</summary>
    D3D12_FENCE,
    MAX_NUM,
}
