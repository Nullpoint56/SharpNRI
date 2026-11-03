namespace SharpNRI.Native;

public unsafe partial struct WriteSamplerDescriptorsDesc
{
    [NativeTypeName("const Descriptor *")]
    public Descriptor* sampler;

    [NativeTypeName("uint32_t")]
    public uint descriptorIndex;
}
