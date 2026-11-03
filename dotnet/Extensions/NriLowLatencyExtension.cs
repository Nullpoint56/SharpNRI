// L3 — the LowLatency extension (NRILowLatency.h).
//
// Minimizing latency between input sampling and presentation (NVIDIA Reflex on D3D12 through
// NVAPI, VK_NV_low_latency2 on Vulkan). Available when DeviceDesc.features.lowLatency is set;
// UNSUPPORTED otherwise. Every call acts on a swap chain created with ALLOW_LOW_LATENCY.
//
// NRI's contract, from the header:
//  - multi-swapchain is supported only by VK;
//  - every work submission of a frame must name the swap chain (QueueSubmitDesc::swapChain), which
//    is NriQueue.Submit(..., swapChain, presentId);
//  - presentId must be non-zero and match the value passed to Submit and QueuePresent for the
//    frame.
// Threadsafe per NRI.

using SharpNRI.Interop;
using SharpNRI.Native;

namespace SharpNRI;

/// <summary>NRI's <c>LowLatencyInterface</c>. Acquire through <see cref="NriDevice.LowLatency"/>.</summary>
public sealed unsafe class NriLowLatencyExtension
{
    private readonly LowLatencyInterface _table;

    internal NriLowLatencyExtension(NriDevice device, in LowLatencyInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriLowLatencyExtension? extension)
    {
        if (InterfaceTable.TryAcquire<LowLatencyInterface>(device.Raw, "LowLatencyInterface", out var table))
        {
            extension = new NriLowLatencyExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// Sets the frame-rate limit (<c>minIntervalUs</c>, 0 for none), low latency mode and its
    /// power-for-latency boost.
    /// </summary>
    public void SetLatencySleepMode(NriSwapChain swapChain, in LatencySleepMode latencySleepMode)
    {
        fixed (LatencySleepMode* p = &latencySleepMode) Device.Check(_table.SetLatencySleepMode(swapChain.Raw, p), "SetLatencySleepMode");
    }

    /// <summary><see cref="SetLatencySleepMode(NriSwapChain, in LatencySleepMode)"/> with the fields inline.</summary>
    public void SetLatencySleepMode(NriSwapChain swapChain, uint minIntervalUs, bool lowLatencyMode, bool lowLatencyBoost) =>
        SetLatencySleepMode(swapChain, new LatencySleepMode
        {
            minIntervalUs = minIntervalUs,
            lowLatencyMode = NriObject.Bool(lowLatencyMode),
            lowLatencyBoost = NriObject.Bool(lowLatencyBoost),
        });

    /// <summary>
    /// Marks a point in the frame identified by <paramref name="presentId"/>; see
    /// <see cref="LatencyMarker"/> for where each one belongs (<c>INPUT_SAMPLE</c> is 6).
    /// </summary>
    public void SetLatencyMarker(NriSwapChain swapChain, ulong presentId, LatencyMarker latencyMarker) =>
        Device.Check(_table.SetLatencyMarker(swapChain.Raw, presentId, latencyMarker), "SetLatencyMarker");

    /// <summary>Sleeps to reduce latency. Call once per frame, before <c>INPUT_SAMPLE</c>.</summary>
    public void LatencySleep(NriSwapChain swapChain, ulong presentId) =>
        Device.Check(_table.LatencySleep(swapChain.Raw, presentId), "LatencySleep");

    /// <summary>Timestamps (microseconds) of the markers and of the driver, OS and GPU stages of a recent frame.</summary>
    public LatencyReport GetLatencyReport(NriSwapChain swapChain)
    {
        LatencyReport report;
        Device.Check(_table.GetLatencyReport(swapChain.Raw, &report), "GetLatencyReport");
        return report;
    }
}
