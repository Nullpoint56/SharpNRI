namespace SharpNRI.Native;

/// <summary>requires "pipelineLayout.rootDescriptorMaxNum &gt; 0", or "descriptorHeap.rootDescriptorMaxNum &gt; 0" in "descriptor heap" mode</summary>
public unsafe partial struct SetRootDescriptorDesc
{
    /// <summary>an index in "PipelineLayoutDesc::rootDescriptors"</summary>
    [NativeTypeName("uint32_t")]
    public uint rootDescriptorIndex;

    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;

    /// <summary>a non-"CONSTANT_BUFFER" descriptor requires "features.nonConstantBufferRootDescriptorOffset"</summary>
    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("nri::BindPoint")]
    public BindPoint bindPoint;
}
