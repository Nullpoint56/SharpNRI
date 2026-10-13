namespace PrismNRI.Native;

public partial struct GlobalBarrierDesc
{
    [NativeTypeName("nri::AccessStage")]
    public AccessStage before;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage after;
}
