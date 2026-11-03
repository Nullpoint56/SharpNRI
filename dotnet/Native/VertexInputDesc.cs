namespace SharpNRI.Native;

public unsafe partial struct VertexInputDesc
{
    [NativeTypeName("const VertexAttributeDesc *")]
    public VertexAttributeDesc* attributes;

    [NativeTypeName("uint8_t")]
    public byte attributeNum;

    [NativeTypeName("const VertexStreamDesc *")]
    public VertexStreamDesc* streams;

    [NativeTypeName("uint8_t")]
    public byte streamNum;
}
