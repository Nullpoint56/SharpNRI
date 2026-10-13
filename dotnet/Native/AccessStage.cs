namespace PrismNRI.Native;

public partial struct AccessStage
{
    [NativeTypeName("nri::AccessBits")]
    public AccessBits access;

    [NativeTypeName("nri::StageBits")]
    public StageBits stages;
}
