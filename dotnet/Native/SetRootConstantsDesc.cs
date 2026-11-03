namespace SharpNRI.Native;

public unsafe partial struct SetRootConstantsDesc
{
    [NativeTypeName("uint32_t")]
    public uint rootConstantIndex;

    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("nri::BindPoint")]
    public BindPoint bindPoint;
}
