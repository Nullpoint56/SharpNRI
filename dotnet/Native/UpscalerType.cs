namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum UpscalerType : byte
{
    /// <summary>NVIDIA Image Scaling sharpener-upscaler, cross vendor</summary>
    NIS,
    /// <summary>AMD FidelityFX Super Resolution FSR 4 where supported, FSR 3 otherwise</summary>
    FSR,
    /// <summary>INTEL XeSS Super Resolution upscaler, cross vendor</summary>
    XESS,
    /// <summary>NVIDIA Deep Learning Super Resolution upscaler, NVIDIA only</summary>
    DLSR,
    /// <summary>NVIDIA Deep Learning Ray Reconstruction upscaler-denoiser, NVIDIA only</summary>
    DLRR,
    /// <summary>Apple MetalFX spatial upscaler upscaler, Metal only</summary>
    METALFX_SPATIAL,
    /// <summary>Apple MetalFX temporal upscaler upscaler, Metal only</summary>
    METALFX_TEMPORAL,
    /// <summary>Apple MetalFX upscaler-denoiser upscaler-denoiser, Metal only</summary>
    METALFX_DENOISER,
    MAX_NUM,
}
