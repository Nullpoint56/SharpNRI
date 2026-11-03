namespace SharpNRI.Native;

public partial struct ClearAttachmentDesc
{
    [NativeTypeName("nri::ClearValue")]
    public ClearValue value;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;

    [NativeTypeName("uint8_t")]
    public byte colorAttachmentIndex;
}
