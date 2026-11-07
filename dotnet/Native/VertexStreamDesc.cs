namespace SharpNRI.Native;

public partial struct VertexStreamDesc
{
    [NativeTypeName("uint16_t")]
    public ushort bindingSlot;

    [NativeTypeName("nri::VertexStreamStepRate")]
    public VertexStreamStepRate stepRate;

    /// <summary>fallback if "features.extendedDynamicState" is not supported</summary>
    [NativeTypeName("uint16_t")]
    public ushort stride;
}
