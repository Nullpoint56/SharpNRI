namespace SharpNRI.Native;

public unsafe partial struct WriteResourceDescriptorsDesc
{
    /// <summary>any descriptor except "MUTABLE", "INPUT_ATTACHMENT" and "SAMPLER"</summary>
    [NativeTypeName("const Descriptor *")]
    public Descriptor* resource;

    [NativeTypeName("uint32_t")]
    public uint descriptorIndex;
}
