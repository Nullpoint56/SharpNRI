namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum LatencyMarker : byte
{
    /// <summary>at the start of the simulation execution each frame, but after the call to "LatencySleep"</summary>
    SIMULATION_START = 0,
    /// <summary>at the end of the simulation execution each frame</summary>
    SIMULATION_END = 1,
    /// <summary>at the beginning of the render submission execution each frame (must not span into asynchronous rendering)</summary>
    RENDER_SUBMIT_START = 2,
    /// <summary>at the end of the render submission execution each frame</summary>
    RENDER_SUBMIT_END = 3,
    /// <summary>just before the application gathers input data, but between "SIMULATION_START" and "SIMULATION_END" (yes, 6!)</summary>
    INPUT_SAMPLE = 6,
    MAX_NUM,
}
