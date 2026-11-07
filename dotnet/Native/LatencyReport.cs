namespace SharpNRI.Native;

/// <summary>The time stamp written:</summary>
public partial struct LatencyReport
{
    /// <summary>when "INPUT_SAMPLE" marker is set</summary>
    [NativeTypeName("uint64_t")]
    public ulong inputSampleTimeUs;

    /// <summary>when "SIMULATION_START" marker is set</summary>
    [NativeTypeName("uint64_t")]
    public ulong simulationStartTimeUs;

    /// <summary>when "SIMULATION_END" marker is set</summary>
    [NativeTypeName("uint64_t")]
    public ulong simulationEndTimeUs;

    /// <summary>when "RENDER_SUBMIT_START" marker is set</summary>
    [NativeTypeName("uint64_t")]
    public ulong renderSubmitStartTimeUs;

    /// <summary>when "RENDER_SUBMIT_END" marker is set</summary>
    [NativeTypeName("uint64_t")]
    public ulong renderSubmitEndTimeUs;

    /// <summary>right before "Present"</summary>
    [NativeTypeName("uint64_t")]
    public ulong presentStartTimeUs;

    /// <summary>right after "Present"</summary>
    [NativeTypeName("uint64_t")]
    public ulong presentEndTimeUs;

    /// <summary>when the first "QueueSubmitTrackable" is called</summary>
    [NativeTypeName("uint64_t")]
    public ulong driverStartTimeUs;

    /// <summary>when the final "QueueSubmitTrackable" hands off from the driver</summary>
    [NativeTypeName("uint64_t")]
    public ulong driverEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong osRenderQueueStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong osRenderQueueEndTimeUs;

    /// <summary>when the first submission reaches the GPU</summary>
    [NativeTypeName("uint64_t")]
    public ulong gpuRenderStartTimeUs;

    /// <summary>when the final submission finishes on the GPU</summary>
    [NativeTypeName("uint64_t")]
    public ulong gpuRenderEndTimeUs;
}
