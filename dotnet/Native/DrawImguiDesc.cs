namespace SharpNRI.Native;

public partial struct DrawImguiDesc
{
    /// <summary>ImDrawData::DisplaySize</summary>
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t displaySize;

    /// <summary>SDR intensity in HDR mode (1 by default)</summary>
    public float hdrScale;

    /// <summary>destination attachment (render target) format</summary>
    [NativeTypeName("nri::Format")]
    public Format attachmentFormat;

    /// <summary>apply de-gamma to vertex colors (needed for sRGB attachments and HDR)</summary>
    [NativeTypeName("bool")]
    public byte linearColor;
}
