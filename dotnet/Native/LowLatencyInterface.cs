namespace SharpNRI.Native;

/// <summary>Multi-swapchain is supported only by VK "QueueSubmitDesc::swapChain" must be used to associate work submissions with a low latency swap chain "presentId" must be non-zero and match the value passed to "QueueSubmit" and "QueuePresent" for the frame Threadsafe: yes</summary>
public unsafe partial struct LowLatencyInterface
{
    [NativeTypeName("Result (*)(SwapChain &, const LatencySleepMode &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, LatencySleepMode*, Result> SetLatencySleepMode;

    [NativeTypeName("Result (*)(SwapChain &, uint64_t, LatencyMarker) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, ulong, LatencyMarker, Result> SetLatencyMarker;

    /// <summary>call once before "INPUT_SAMPLE"</summary>
    [NativeTypeName("Result (*)(SwapChain &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, ulong, Result> LatencySleep;

    [NativeTypeName("Result (*)(const SwapChain &, LatencyReport &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, LatencyReport*, Result> GetLatencyReport;
}
