namespace SharpNRI.Native;

public partial struct VertexAttributeDesc
{
    [NativeTypeName("nri::VertexAttributeD3D")]
    public VertexAttributeD3D d3d;

    [NativeTypeName("nri::VertexAttributeVK")]
    public VertexAttributeVK vk;

    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("nri::Format")]
    public Format format;

    [NativeTypeName("uint16_t")]
    public ushort streamIndex;
}
