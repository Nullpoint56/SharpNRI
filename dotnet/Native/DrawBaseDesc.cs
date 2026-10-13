namespace PrismNRI.Native;

public partial struct DrawBaseDesc
{
    [NativeTypeName("uint32_t")]
    public uint shaderEmulatedBaseVertex;

    [NativeTypeName("uint32_t")]
    public uint shaderEmulatedBaseInstance;

    [NativeTypeName("uint32_t")]
    public uint vertexNum;

    [NativeTypeName("uint32_t")]
    public uint instanceNum;

    [NativeTypeName("uint32_t")]
    public uint baseVertex;

    [NativeTypeName("uint32_t")]
    public uint baseInstance;
}
