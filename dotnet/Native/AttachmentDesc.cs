namespace SharpNRI.Native;

public unsafe partial struct AttachmentDesc
{
    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;

    [NativeTypeName("nri::ClearValue")]
    public ClearValue clearValue;

    [NativeTypeName("nri::LoadOp")]
    public LoadOp loadOp;

    [NativeTypeName("nri::StoreOp")]
    public StoreOp storeOp;

    [NativeTypeName("nri::ResolveOp")]
    public ResolveOp resolveOp;

    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* resolveDst;
}
