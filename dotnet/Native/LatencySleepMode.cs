namespace SharpNRI.Native;

public partial struct LatencySleepMode
{
    [NativeTypeName("uint32_t")]
    public uint minIntervalUs;

    [NativeTypeName("bool")]
    public byte lowLatencyMode;

    [NativeTypeName("bool")]
    public byte lowLatencyBoost;
}
