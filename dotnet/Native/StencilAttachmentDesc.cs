namespace SharpNRI.Native;

public partial struct StencilAttachmentDesc
{
    [NativeTypeName("nri::StencilDesc")]
    public StencilDesc front;

    /// <summary>requires "features.independentFrontAndBackStencilReferenceAndMasks" for "back.writeMask"</summary>
    [NativeTypeName("nri::StencilDesc")]
    public StencilDesc back;
}
