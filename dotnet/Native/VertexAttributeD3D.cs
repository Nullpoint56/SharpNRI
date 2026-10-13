namespace PrismNRI.Native;

public unsafe partial struct VertexAttributeD3D
{
    [NativeTypeName("const char *")]
    public sbyte* semanticName;

    [NativeTypeName("uint32_t")]
    public uint semanticIndex;
}
