namespace SharpNRI.Native;

[NativeTypeName("uint16_t")]
[Flags]
public enum UpscalerBits : ushort
{
    NONE = 0,
    /// <summary>"input" uses colors in High-Dynamic Range (HDR)</summary>
    HDR = (1 << 0),
    /// <summary>"input" uses Low-Dynamic Range (LDR) colors in sRGB space</summary>
    SRGB = (1 << 1),
    /// <summary>"exposure" texture is provided (automatic exposure otherwise)</summary>
    USE_EXPOSURE = (1 << 2),
    /// <summary>"reactive" texture is provided</summary>
    USE_REACTIVE = (1 << 3),
    /// <summary>"depth" is inverted, i.e. the near plane is mapped to 1</summary>
    DEPTH_INVERTED = (1 << 4),
    /// <summary>"depth" uses INF far plane</summary>
    DEPTH_INFINITE = (1 << 5),
    /// <summary>"depth" is linear viewZ (HW otherwise) (unsupported by METALFX_TEMPORAL and METALFX_DENOISER)</summary>
    DEPTH_LINEAR = (1 << 6),
    /// <summary>"mv" are rendered at upscale resolution (METALFX_TEMPORAL requires macOS 27, unsupported by METALFX_DENOISER)</summary>
    MV_UPSCALED = (1 << 7),
    /// <summary>"mv" include jitter (unsupported by METALFX_TEMPORAL and METALFX_DENOISER)</summary>
    MV_JITTERED = (1 << 8),
}
