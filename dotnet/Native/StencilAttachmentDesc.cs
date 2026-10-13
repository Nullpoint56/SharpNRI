namespace PrismNRI.Native;

public partial struct StencilAttachmentDesc
{
    [NativeTypeName("nri::StencilDesc")]
    public StencilDesc front;

    [NativeTypeName("nri::StencilDesc")]
    public StencilDesc back;
}
