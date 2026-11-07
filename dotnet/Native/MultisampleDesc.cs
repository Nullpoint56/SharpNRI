namespace SharpNRI.Native;

public partial struct MultisampleDesc
{
    /// <summary>can be "ALL" (METAL: native fragment shaders must apply "NriApplyPipelineSampleMask", see "NRI.metal")</summary>
    [NativeTypeName("uint32_t")]
    public uint sampleMask;

    [NativeTypeName("nri::Sample_t")]
    public byte sampleNum;

    [NativeTypeName("bool")]
    public byte alphaToCoverage;

    /// <summary>requires "tiers.sampleLocations != 0", expects "CmdSetSampleLocations"</summary>
    [NativeTypeName("bool")]
    public byte sampleLocations;
}
