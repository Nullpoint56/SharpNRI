namespace PrismNRI.Native;

public partial struct VertexStreamDesc
{
    [NativeTypeName("uint16_t")]
    public ushort bindingSlot;

    [NativeTypeName("nri::VertexStreamStepRate")]
    public VertexStreamStepRate stepRate;

    [NativeTypeName("uint16_t")]
    public ushort stride;
}
