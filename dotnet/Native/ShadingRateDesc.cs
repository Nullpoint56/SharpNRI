namespace SharpNRI.Native;

public partial struct ShadingRateDesc
{
    [NativeTypeName("nri::ShadingRate")]
    public ShadingRate shadingRate;

    [NativeTypeName("nri::ShadingRateCombiner")]
    public ShadingRateCombiner primitiveCombiner;

    [NativeTypeName("nri::ShadingRateCombiner")]
    public ShadingRateCombiner attachmentCombiner;
}
