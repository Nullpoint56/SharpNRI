namespace SharpNRI.Native;

public partial struct UpscalerProps
{
    /// <summary>scaling factor per dimension</summary>
    public float scalingFactor;

    /// <summary>mip bias for material textures, computed as "-log2(scalingFactor) - 1" (keep an eye on normal maps)</summary>
    public float mipBias;

    /// <summary>output resolution</summary>
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t upscaleResolution;

    /// <summary>optimal render resolution</summary>
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t renderResolution;

    /// <summary>minimum render resolution (for dynamic resolution scaling)</summary>
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t renderResolutionMin;

    /// <summary>minimum number of phases in the jitter sequence, computed as "ceil(8 * scalingFactor ^ 2)" ("Halton(2, 3)" recommended)</summary>
    [NativeTypeName("uint8_t")]
    public byte jitterPhaseNum;
}
