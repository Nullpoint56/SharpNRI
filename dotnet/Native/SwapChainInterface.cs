namespace SharpNRI.Native;

/// <summary>Threadsafe: yes</summary>
public unsafe partial struct SwapChainInterface
{
    [NativeTypeName("Result (*)(Device &, const SwapChainDesc &, SwapChain *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, SwapChainDesc*, SwapChain**, Result> CreateSwapChain;

    [NativeTypeName("void (*)(SwapChain *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, void> DestroySwapChain;

    [NativeTypeName("Texture *const *(*)(const SwapChain &, uint32_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, uint*, Texture**> GetSwapChainTextures;

    /// <summary>Returns "FAILURE" if swap chain's window is outside of all monitors</summary>
    [NativeTypeName("Result (*)(SwapChain &, DisplayDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, DisplayDesc*, Result> GetDisplayDesc;

    /// <summary>"acquireSemaphore" must be created with "SWAPCHAIN_SEMAPHORE" initial value (ignored in D3D and WGPU). VK/WGPU: may return "OUT_OF_DATE" WGPU: "NOT_READY" means no texture was acquired; skip the frame, process window events and retry without recreating the swap chain</summary>
    [NativeTypeName("Result (*)(SwapChain &, Fence &, uint32_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, Fence*, uint*, Result> AcquireNextTexture;

    /// <summary>"presentId" must identify a previously queued presentation. Call once immediately before input sampling</summary>
    [NativeTypeName("Result (*)(SwapChain &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, ulong, Result> WaitForPresent;

    /// <summary>A non-zero "presentId" associates the presentation with a tracked frame. Use 0 for an untracked presentation. Non-zero "presentId" values must strictly increase across frames for each swap chain, i.e. gaps are allowed "releaseSemaphore" must be created with "SWAPCHAIN_SEMAPHORE" initial value (ignored in D3D)</summary>
    [NativeTypeName("Result (*)(SwapChain &, Fence &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, Fence*, ulong, Result> QueuePresent;
}
