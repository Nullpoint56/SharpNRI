namespace PrismNRI.Native;

public unsafe partial struct ShaderDesc
{
    [NativeTypeName("nri::StageBits")]
    public StageBits stage;

    [NativeTypeName("const void *")]
    public void* bytecode;

    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("const char *")]
    public sbyte* entryPointName;

    [NativeTypeName("nri::Dim_t")]
    public ushort threadGroupSizeX;

    [NativeTypeName("nri::Dim_t")]
    public ushort threadGroupSizeY;

    [NativeTypeName("nri::Dim_t")]
    public ushort threadGroupSizeZ;
}
