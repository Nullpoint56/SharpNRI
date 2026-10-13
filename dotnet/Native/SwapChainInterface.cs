namespace PrismNRI.Native;

public unsafe partial struct SwapChainInterface
{
    [NativeTypeName("Result (*)(Device &, const SwapChainDesc &, SwapChain *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, SwapChainDesc*, SwapChain**, Result> CreateSwapChain;

    [NativeTypeName("void (*)(SwapChain *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, void> DestroySwapChain;

    [NativeTypeName("Texture *const *(*)(const SwapChain &, uint32_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, uint*, Texture**> GetSwapChainTextures;

    [NativeTypeName("Result (*)(SwapChain &, DisplayDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, DisplayDesc*, Result> GetDisplayDesc;

    [NativeTypeName("Result (*)(SwapChain &, Fence &, uint32_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, Fence*, uint*, Result> AcquireNextTexture;

    [NativeTypeName("Result (*)(SwapChain &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, ulong, Result> WaitForPresent;

    [NativeTypeName("Result (*)(SwapChain &, Fence &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, Fence*, ulong, Result> QueuePresent;
}
