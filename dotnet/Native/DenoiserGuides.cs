namespace SharpNRI.Native;

/// <summary>For DLRR, METALFX_DENOISER</summary>
public partial struct DenoiserGuides
{
    /// <summary>xy - surface motion</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource mv;

    /// <summary>x - hardware depth (DLRR also supports linear view-space Z via "DEPTH_LINEAR")</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource depth;

    /// <summary>xyz - world-space normal (not encoded), .w - linear roughness</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource normalRoughness;

    /// <summary>xyz - diffuse albedo (LDR sky color for sky)</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource diffuseAlbedo;

    /// <summary>xyz - specular albedo (environment BRDF)</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource specularAlbedo;

    /// <summary>xy - specular virtual motion when "USE_SPECULAR_MOTION" is set; .x - specular hit distance otherwise (METALFX_DENOISER requires hit distance)</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource specularMvOrHitT;

    /// <summary>x - 1x1 exposure</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource exposure;

    /// <summary>x - bias towards "input"</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource reactive;

    /// <summary>DLRR only xyz - color before transparency (aka transparency guide)</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource preTransparency;

    /// <summary>x - subsurface scattering, computed as "Luminance(colorAfterSSS - colorBeforeSSS)"</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource sss;
}
