namespace SharpNRI.Native;

public unsafe partial struct WriteResourceDescriptorsDesc
{
    [NativeTypeName("const Descriptor *")]
    public Descriptor* resource;

    [NativeTypeName("uint32_t")]
    public uint descriptorIndex;
}
