using System.Runtime.InteropServices;

namespace SharpNRI.Native;

public partial struct DispatchUpscaleDesc
{
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource output;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource input;

    [NativeTypeName("__AnonymousRecord_NRIUpscaler_L127_C5")]
    public _guides_e__Union guides;

    [NativeTypeName("__AnonymousRecord_NRIUpscaler_L133_C5")]
    public _settings_e__Union settings;

    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t currentResolution;

    [NativeTypeName("nri::Float2_t")]
    public Float2_t cameraJitter;

    [NativeTypeName("nri::Float2_t")]
    public Float2_t mvScale;

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
