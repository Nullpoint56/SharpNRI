using System.Runtime.InteropServices;

namespace SharpNRI.Native;

public partial struct DispatchUpscaleDesc
{
    /// <summary>Output (requires "SHADER_RESOURCE_STORAGE" state and a "STORAGE_TEXTURE" descriptor) - MetalFX: requires "COLOR_ATTACHMENT" usage xyz - upscaled RGB color</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource output;

    /// <summary>Input (requires "SHADER_RESOURCE" state and a "TEXTURE" descriptor) xyz - input RGB color</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource input;

    [NativeTypeName("__AnonymousRecord_NRIUpscaler_L127_C5")]
    public _guides_e__Union guides;

    [NativeTypeName("__AnonymousRecord_NRIUpscaler_L133_C5")]
    public _settings_e__Union settings;

    /// <summary>active input resolution, renderResolutionMin &lt;= currentResolution &lt;= renderResolution</summary>
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t currentResolution;

    /// <summary>jitter offset in pixels, towards the pixel center, each component in [-0.5; 0.5]</summary>
    [NativeTypeName("nri::Float2_t")]
    public Float2_t cameraJitter;

    /// <summary>component-wise multiplier converting motion vectors to pixel space</summary>
    [NativeTypeName("nri::Float2_t")]
    public Float2_t mvScale;

    /// <summary>multiplier already applied to the "input" color, 0 means 1 (FSR, XESS, DLSR, DLRR, METALFX_TEMPORAL, METALFX_DENOISER)</summary>
    public float preExposure;

    [NativeTypeName("nri::DispatchUpscaleBits")]
    public DispatchUpscaleBits flags;

    [StructLayout(LayoutKind.Explicit)]
    public partial struct _guides_e__Union
    {
        [FieldOffset(0)]
        [NativeTypeName("nri::UpscalerGuides")]
        public UpscalerGuides upscaler;

        [FieldOffset(0)]
        [NativeTypeName("nri::DenoiserGuides")]
        public DenoiserGuides denoiser;
    }

    [StructLayout(LayoutKind.Explicit)]
    public partial struct _settings_e__Union
    {
        [FieldOffset(0)]
        [NativeTypeName("nri::NISSettings")]
        public NISSettings nis;

        [FieldOffset(0)]
        [NativeTypeName("nri::FSRSettings")]
        public FSRSettings fsr;

        [FieldOffset(0)]
        [NativeTypeName("nri::DLRRSettings")]
        public DLRRSettings dlrr;
    }
}
