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

    /// <summary>requires "features.lineSmoothing"</summary>
    [NativeTypeName("bool")]
    public byte lineSmoothing;

    /// <summary>requires "tiers.conservativeRaster != 0"</summary>
    [NativeTypeName("bool")]
    public byte conservativeRaster;

    /// <summary>requires "tiers.shadingRate != 0", expects "CmdSetShadingRate" and optionally "RenderingDesc::shadingRate"</summary>
    [NativeTypeName("bool")]
    public byte shadingRate;
}
