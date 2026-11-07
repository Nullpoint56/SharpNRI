namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum SwapChainBits : byte
{
    NONE = 0,
    VSYNC = (1 << 0),
    WAITABLE = (1 << 1),
    ALLOW_TEARING = (1 << 2),
    ALLOW_LOW_LATENCY = (1 << 3),
    LATEST_READY = (1 << 4),
}
