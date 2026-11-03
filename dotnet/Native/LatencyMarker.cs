namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum LatencyMarker : byte
{
    SIMULATION_START = 0,
    SIMULATION_END = 1,
    RENDER_SUBMIT_START = 2,
    RENDER_SUBMIT_END = 3,
    INPUT_SAMPLE = 6,
    MAX_NUM,
}
