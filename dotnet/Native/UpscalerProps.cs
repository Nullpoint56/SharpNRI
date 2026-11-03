namespace SharpNRI.Native;

public partial struct UpscalerProps
{
    public float scalingFactor;

    public float mipBias;

    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t upscaleResolution;

    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t renderResolution;

    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t renderResolutionMin;

    [NativeTypeName("uint8_t")]
    public byte jitterPhaseNum;
}
