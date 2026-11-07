namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum DispatchUpscaleBits : byte
{
    NONE = 0,
    /// <summary>restart accumulation</summary>
    RESET_HISTORY = (1 << 0),
    /// <summary>("DLRR" only) if set, "specularMvOrHitT" represents "specular motion" not "hit distance"</summary>
    USE_SPECULAR_MOTION = (1 << 1),
}
