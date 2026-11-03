namespace SharpNRI.Native;

public partial struct DrawIndexedBaseDesc
{
    [NativeTypeName("int32_t")]
    public int shaderEmulatedBaseVertex;

    [NativeTypeName("uint32_t")]
    public uint shaderEmulatedBaseInstance;

    [NativeTypeName("uint32_t")]
    public uint indexNum;

    [NativeTypeName("uint32_t")]
    public uint instanceNum;

    [NativeTypeName("uint32_t")]
    public uint baseIndex;

    [NativeTypeName("int32_t")]
    public int baseVertex;

    [NativeTypeName("uint32_t")]
    public uint baseInstance;
}
