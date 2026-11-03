namespace SharpNRI.Native;

public partial struct UpscalerGuides
{
    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource mv;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource depth;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource exposure;

    [NativeTypeName("nri::UpscalerResource")]
    public UpscalerResource reactive;
}
