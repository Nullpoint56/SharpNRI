namespace SharpNRI.Native;

public partial struct DepthStencil
{
    public float depth;

    [NativeTypeName("uint8_t")]
    public byte stencil;
}
