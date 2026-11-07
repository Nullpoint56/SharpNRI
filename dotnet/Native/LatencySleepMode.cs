namespace SharpNRI.Native;

public partial struct LatencySleepMode
{
    /// <summary>minimum allowed frame interval (0 - no frame rate limit)</summary>
    [NativeTypeName("uint32_t")]
    public uint minIntervalUs;

    /// <summary>low latency mode enablement</summary>
    [NativeTypeName("bool")]
    public byte lowLatencyMode;

    /// <summary>hint to increase performance to provide additional latency savings at a cost of increased power consumption</summary>
    [NativeTypeName("bool")]
    public byte lowLatencyBoost;
}
