using System.Runtime.InteropServices;

namespace PrismNRI.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct ClearValue
{
    [FieldOffset(0)]
    [NativeTypeName("nri::DepthStencil")]
    public DepthStencil depthStencil;

    [FieldOffset(0)]
    [NativeTypeName("nri::Color")]
    public Color color;
}
