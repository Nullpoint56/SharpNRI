namespace SharpNRI.Native;

/// <summary>Guide textures For FSR, XESS, DLSR, METALFX_TEMPORAL</summary>
public partial struct UpscalerGuides
{
    /// <summary>xy - surface motion</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource mv;

    /// <summary>x - HW depth</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource depth;

    /// <summary>x - 1x1 exposure</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource exposure;

    /// <summary>x - bias towards "input"</summary>
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource reactive;
}
