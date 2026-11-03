namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum UpscalerType : byte
{
    NIS,
    FSR,
    XESS,
    DLSR,
    DLRR,
    METALFX_SPATIAL,
    METALFX_TEMPORAL,
    METALFX_DENOISER,
    MAX_NUM,
}
