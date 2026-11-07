namespace SharpNRI.Native;

/// <summary>requires "pipelineLayout.rootConstantMaxSize &gt; 0", or "descriptorHeap.rootConstantMaxSize &gt; 0" in "descriptor heap" mode</summary>
public unsafe partial struct SetRootConstantsDesc
{
    /// <summary>an index in "PipelineLayoutDesc::rootConstants"</summary>
    [NativeTypeName("uint32_t")]
    public uint rootConstantIndex;

    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("uint32_t")]
    public uint size;

    /// <summary>requires "features.rootConstantsOffset"</summary>
    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("nri::BindPoint")]
    public BindPoint bindPoint;
}
