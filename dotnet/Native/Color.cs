using System.Runtime.InteropServices;

namespace SharpNRI.Native;

[StructLayout(LayoutKind.Explicit)]
public partial struct Color
{
    [FieldOffset(0)]
    [NativeTypeName("nri::Color32f")]
    public Color32f f;

    [FieldOffset(0)]
    [NativeTypeName("nri::Color32ui")]
    public Color32ui ui;

    [FieldOffset(0)]
    [NativeTypeName("nri::Color32i")]
    public Color32i i;
}
