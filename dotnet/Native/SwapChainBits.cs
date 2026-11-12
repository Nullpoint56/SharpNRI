namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum SwapChainBits : byte
{
    NONE = 0,
    /// <summary>cap framerate to the monitor refresh rate (except with "LATEST_READY")</summary>
    VSYNC = (1 << 0),
    /// <summary>unlock "WaitForPresent" reducing latency (requires "features.waitableSwapChain")</summary>
    WAITABLE = (1 << 1),
    /// <summary>allow screen tearing if possible (METAL: "displaySyncEnabled = NO", may show partially rendered frames)</summary>
    ALLOW_TEARING = (1 << 2),
    /// <summary>allow "NRILowLatency" functionality (requires "features.lowLatency")</summary>
    ALLOW_LOW_LATENCY = (1 << 3),
    /// <summary>Multiple ready frames may be consumed per vblank, discarding older frames; rendering can exceed the monitor refresh rate. Silently ignored if not supported VK: with "VSYNC" prefer "FIFO_LATEST_READY" (the newest ready image at VBLANK, no tearing) over "FIFO" (requires "features.latestReadySwapChain")</summary>
    LATEST_READY = (1 << 4),
}
