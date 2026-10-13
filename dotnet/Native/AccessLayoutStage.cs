namespace PrismNRI.Native;

public partial struct AccessLayoutStage
{
    [NativeTypeName("nri::AccessBits")]
    public AccessBits access;

    [NativeTypeName("nri::Layout")]
    public Layout layout;

    [NativeTypeName("nri::StageBits")]
    public StageBits stages;
}
