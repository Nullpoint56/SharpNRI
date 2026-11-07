namespace SharpNRI.Native;

/// <summary>Use largest offset for the resource type planned to be used as an unbounded array</summary>
public partial struct VKBindingOffsets
{
    /// <summary>samplers</summary>
    [NativeTypeName("uint32_t")]
    public uint sRegister;

    /// <summary>shader resources, including acceleration structures (SRVs)</summary>
    [NativeTypeName("uint32_t")]
    public uint tRegister;

    /// <summary>constant buffers</summary>
    [NativeTypeName("uint32_t")]
    public uint bRegister;

    /// <summary>storage shader resources (UAVs)</summary>
    [NativeTypeName("uint32_t")]
    public uint uRegister;
}
