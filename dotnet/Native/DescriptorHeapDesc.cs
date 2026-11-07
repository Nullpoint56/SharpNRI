namespace SharpNRI.Native;

/// <summary>At least one capacity must be non-zero</summary>
public partial struct DescriptorHeapDesc
{
    /// <summary>0 - no resource heap</summary>
    [NativeTypeName("uint32_t")]
    public uint resourceDescriptorNum;

    /// <summary>0 - no sampler heap</summary>
    [NativeTypeName("uint32_t")]
    public uint samplerDescriptorNum;
}
