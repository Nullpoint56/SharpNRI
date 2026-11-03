namespace SharpNRI.Native;

public unsafe partial struct SetRootDescriptorDesc
{
    [NativeTypeName("uint32_t")]
    public uint rootDescriptorIndex;

    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;

    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("nri::BindPoint")]
    public BindPoint bindPoint;
}
