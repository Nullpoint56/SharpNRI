namespace SharpNRI.Native;

/// <summary>Expects "NRI_ENABLE_WAYLAND_SUPPORT"</summary>
public unsafe partial struct WaylandWindow
{
    /// <summary>wl_display</summary>
    public void* display;

    /// <summary>wl_surface</summary>
    public void* surface;
}
