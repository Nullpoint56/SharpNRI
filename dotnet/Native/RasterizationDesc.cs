namespace SharpNRI.Native;

public partial struct RasterizationDesc
{
    [NativeTypeName("nri::DepthBiasDesc")]
    public DepthBiasDesc depthBias;

    [NativeTypeName("nri::FillMode")]
    public FillMode fillMode;

    [NativeTypeName("nri::CullMode")]
    public CullMode cullMode;

    [NativeTypeName("bool")]
    public byte frontCounterClockwise;

    [NativeTypeName("bool")]
    public byte depthClamp;

    [NativeTypeName("bool")]
    public byte lineSmoothing;

    [NativeTypeName("bool")]
    public byte conservativeRaster;

    [NativeTypeName("bool")]
    public byte shadingRate;
}
