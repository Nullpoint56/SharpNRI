namespace SharpNRI.Native;

public unsafe partial struct TextureViewDesc
{
    [NativeTypeName("const Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::TextureView")]
    public TextureView type;

    /// <summary>inherits "TextureDesc::format" if "UNKNOWN"</summary>
    [NativeTypeName("nri::Format")]
    public Format format;

    [NativeTypeName("nri::Dim_t")]
    public ushort mipOffset;

    /// <summary>can be "REMAINING"</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort mipNum;

    /// <summary>0 for "TEXTURE_3D" (D3D11: must be 0 for "STORAGE_TEXTURE")</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort layerOffset;

    /// <summary>can be "REMAINING"; array views may expose 1 layer (6 for cube arrays)</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort layerNum;

    [NativeTypeName("nri::Dim_t")]
    public ushort sliceOffset;

    /// <summary>can be "REMAINING" (METAL: used only by "CmdClearStorage", shader views expose all slices, attachments use "sliceOffset")</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort sliceNum;

    /// <summary>accessible planes (missing planes are read-only for "DEPTH_STENCIL_ATTACHMENT" if "layout" is not provided, i.e. "UNDEFINED")</summary>
    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;

    [NativeTypeName("nri::ComponentMapping")]
    public ComponentMapping components;

    /// <summary>Overrides the expected access-time layout without transitioning the texture. May be any layout compatible with the view and texture usage, including fully or partially read-only depth-stencil layouts auto-derived from the "type" and "planes" if "UNDEFINED"</summary>
    [NativeTypeName("nri::Layout")]
    public Layout layout;
}
