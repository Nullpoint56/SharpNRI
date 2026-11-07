namespace SharpNRI.Native;

/// <summary>Required synchronization - variant 1: "SHADER_RESOURCE_STORAGE" access ("SHADER_RESOURCE_STORAGE" layout) and "CLEAR_STORAGE" stage + any shader stage (or "ALL") - variant 2: "CLEAR_STORAGE" access ("SHADER_RESOURCE_STORAGE" layout) and "CLEAR_STORAGE" stage</summary>
public unsafe partial struct ClearStorageDesc
{
    /// <summary>For any buffers and textures with integer formats: - Clears a storage descriptor with bit-precise values, copying the lower "N" bits from "value.[f/ui/i].channel" to the corresponding channel, where "N" is the number of bits in the "channel" of the resource format For textures with non-integer formats: - Clears a storage descriptor with float values with format conversion from "FLOAT" to "UNORM/SNORM" where appropriate For buffers: - To avoid discrepancies in behavior between GAPIs use "R32f/ui/i" formats for views - D3D: structured buffers are unsupported! a "STORAGE" descriptor</summary>
    [NativeTypeName("nri::Descriptor *")]
    public Descriptor* descriptor;

    /// <summary>avoid overflow</summary>
    [NativeTypeName("nri::Color")]
    public Color value;

    [NativeTypeName("uint32_t")]
    public uint setIndex;

    [NativeTypeName("uint32_t")]
    public uint rangeIndex;

    [NativeTypeName("uint32_t")]
    public uint descriptorIndex;
}
