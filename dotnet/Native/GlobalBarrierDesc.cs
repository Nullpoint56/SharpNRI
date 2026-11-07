namespace SharpNRI.Native;

public partial struct GlobalBarrierDesc
{
    /// <summary>D3D12: global-barrier sides combining resolve and incompatible access scopes may promote stages to "ALL"</summary>
    [NativeTypeName("nri::AccessStage")]
    public AccessStage before;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage after;
}
