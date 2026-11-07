namespace SharpNRI.Native;

public partial struct FenceDesc
{
    /// <summary>"SWAPCHAIN_SEMAPHORE" creates a binary semaphore (see "NRISwapChain.h")</summary>
    [NativeTypeName("uint64_t")]
    public ulong initialValue;

    /// <summary>"GetFenceSharedHandle": requires "features.sharedFences"; D3D12: unsupported for binary semaphores</summary>
    [NativeTypeName("bool")]
    public byte shared;
}
