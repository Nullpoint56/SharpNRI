namespace SharpNRI.Native;

public unsafe partial struct LowLatencyInterface
{
    [NativeTypeName("Result (*)(SwapChain &, const LatencySleepMode &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, LatencySleepMode*, Result> SetLatencySleepMode;

    [NativeTypeName("Result (*)(SwapChain &, uint64_t, LatencyMarker) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, ulong, LatencyMarker, Result> SetLatencyMarker;

    [NativeTypeName("Result (*)(SwapChain &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, ulong, Result> LatencySleep;

    [NativeTypeName("Result (*)(const SwapChain &, LatencyReport &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SwapChain*, LatencyReport*, Result> GetLatencyReport;
}
