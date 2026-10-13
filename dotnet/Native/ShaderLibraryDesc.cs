namespace PrismNRI.Native;

public unsafe partial struct ShaderLibraryDesc
{
    [NativeTypeName("const ShaderDesc *")]
    public ShaderDesc* shaders;

    [NativeTypeName("uint32_t")]
    public uint shaderNum;
}
