namespace PrismNRI.Native;

public unsafe partial struct ClearStorageDesc
{
    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;

    [NativeTypeName("nri::Color")]
    public Color value;

    [NativeTypeName("uint32_t")]
    public uint setIndex;

    [NativeTypeName("uint32_t")]
    public uint rangeIndex;

    [NativeTypeName("uint32_t")]
    public uint descriptorIndex;
}
