namespace SharpNRI.Native;

/// <summary>It's recommended to use "NRI.hlsl" in the shader code</summary>
public unsafe partial struct ShaderDesc
{
    [NativeTypeName("nri::StageBits")]
    public StageBits stage;

    /// <summary>see "features.shaderBytecodeXXX"</summary>
    [NativeTypeName("const void *")]
    public void* bytecode;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("const char *")]
    public sbyte* entryPointName;

    /// <summary>METAL: required for native (metallib) compute, mesh and task shaders (MSL has no "numthreads"), ignored otherwise</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort threadGroupSizeX;

    [NativeTypeName("nri::Dim_t")]
    public ushort threadGroupSizeY;

    [NativeTypeName("nri::Dim_t")]
    public ushort threadGroupSizeZ;
}
