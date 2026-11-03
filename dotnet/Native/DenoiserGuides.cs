namespace SharpNRI.Native;

public partial struct DenoiserGuides
{
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource mv;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource depth;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource normalRoughness;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource diffuseAlbedo;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource specularAlbedo;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource specularMvOrHitT;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource exposure;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource reactive;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource preTransparency;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource sss;
}
