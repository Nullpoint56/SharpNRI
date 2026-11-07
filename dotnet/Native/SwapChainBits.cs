namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum SwapChainBits : byte
{
    NONE = 0,
    /// <summary>cap framerate to the monitor refresh rate</summary>
    VSYNC = (1 << 0),
    /// <summary>unlock "WaitForPresent" reducing latency (requires "features.waitableSwapChain")</summary>
    WAITABLE = (1 << 1),
    /// <summary>allow screen tearing if possible (METAL: "displaySyncEnabled = NO", may show partially rendered frames)</summary>
    ALLOW_TEARING = (1 << 2),
    /// <summary>allow "NRILowLatency" functionality (requires "features.lowLatency")</summary>
    ALLOW_LOW_LATENCY = (1 << 3),
    /// <summary>VK: with "VSYNC" prefer "FIFO_LATEST_READY" (the newest ready image at vblank, no tearing) over "FIFO" (requires "features.latestReadySwapChain", ignored otherwise)</summary>
    LATEST_READY = (1 << 4),
}
