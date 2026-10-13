namespace PrismNRI.Native;

public partial struct MultisampleDesc
{
    [NativeTypeName("uint32_t")]
    public uint sampleMask;

    [NativeTypeName("nri::Sample_t")]
    public byte sampleNum;

    [NativeTypeName("bool")]
    public byte alphaToCoverage;

    [NativeTypeName("bool")]
    public byte sampleLocations;
}
