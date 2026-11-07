namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum UpscalerMode : byte
{
    /// <summary>1.0x 8</summary>
    NATIVE,
    /// <summary>1.3x 14</summary>
    ULTRA_QUALITY,
    /// <summary>1.5x 18</summary>
    QUALITY,
    /// <summary>1.7x 23</summary>
    BALANCED,
    /// <summary>2.0x 32</summary>
    PERFORMANCE,
    /// <summary>3.0x 72</summary>
    ULTRA_PERFORMANCE,
    MAX_NUM,
}
