namespace PrismNRI.Native;

public partial struct DrawDesc
{
    [NativeTypeName("uint32_t")]
    public uint vertexNum;

    [NativeTypeName("uint32_t")]
    public uint instanceNum;

    [NativeTypeName("uint32_t")]
    public uint baseVertex;

    [NativeTypeName("uint32_t")]
    public uint baseInstance;
}
