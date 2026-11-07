namespace SharpNRI.Native;

public unsafe partial struct WriteSamplerDescriptorsDesc
{
    /// <summary>must be a SAMPLER</summary>
    [NativeTypeName("const Descriptor *")]
    public Descriptor* sampler;

    [NativeTypeName("uint32_t")]
    public uint descriptorIndex;
}
