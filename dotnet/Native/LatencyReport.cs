namespace SharpNRI.Native;

public partial struct LatencyReport
{
    [NativeTypeName("uint64_t")]
    public ulong inputSampleTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong simulationStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong simulationEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong renderSubmitStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong renderSubmitEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong presentStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong presentEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong driverStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong driverEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong osRenderQueueStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong osRenderQueueEndTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong gpuRenderStartTimeUs;

    [NativeTypeName("uint64_t")]
    public ulong gpuRenderEndTimeUs;
}
