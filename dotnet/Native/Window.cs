namespace SharpNRI.Native;

public partial struct Window
{
    [NativeTypeName("nri::WindowsWindow")]
    public WindowsWindow windows;

    [NativeTypeName("nri::X11Window")]
    public X11Window x11;

    [NativeTypeName("nri::WaylandWindow")]
    public WaylandWindow wayland;

    [NativeTypeName("nri::MetalWindow")]
    public MetalWindow metal;

    [NativeTypeName("nri::AndroidWindow")]
    public AndroidWindow android;
}
