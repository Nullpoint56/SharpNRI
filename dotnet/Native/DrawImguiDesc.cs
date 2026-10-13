namespace PrismNRI.Native;

public partial struct DrawImguiDesc
{
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t displaySize;

    public float hdrScale;

    [NativeTypeName("nri::Format")]
    public Format attachmentFormat;

    [NativeTypeName("bool")]
    public byte linearColor;
}
