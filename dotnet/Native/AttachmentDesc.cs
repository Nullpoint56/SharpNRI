namespace SharpNRI.Native;

/// <summary>If "resolveDst" is specified, this attachment must be multisampled. Add "RESOLVE_ATTACHMENT_SOURCE" access and "RESOLVE_ATTACHMENT" stage to its drawing accesses and stages</summary>
public unsafe partial struct AttachmentDesc
{
    /// <summary>attachment view</summary>
    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;

    [NativeTypeName("nri::ClearValue")]
    public ClearValue clearValue;

    [NativeTypeName("nri::LoadOp")]
    public LoadOp loadOp;

    [NativeTypeName("nri::StoreOp")]
    public StoreOp storeOp;

    /// <summary>used if "resolveDst" is specified</summary>
    [NativeTypeName("nri::ResolveOp")]
    public ResolveOp resolveOp;

    /// <summary>Prepare with "RESOLVE_ATTACHMENT_DESTINATION" access and "RESOLVE_ATTACHMENT" stage before "CmdBeginRendering". Both "descriptor" and "resolveDst" (if specified) must remain valid through "CmdEndRendering". Resolve views cannot use "GENERAL" or "INPUT_ATTACHMENT" layouts. "SharingMode::SIMULTANEOUS" textures are not supported. single-sample attachment view receiving resolved values from "descriptor"</summary>
    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* resolveDst;
}
