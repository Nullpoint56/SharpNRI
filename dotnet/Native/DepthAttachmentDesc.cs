namespace SharpNRI.Native;

public partial struct DepthAttachmentDesc
{
    [NativeTypeName("nri::CompareOp")]
    public CompareOp compareOp;

    [NativeTypeName("bool")]
    public byte write;

    [NativeTypeName("bool")]
    public byte boundsTest;
}
