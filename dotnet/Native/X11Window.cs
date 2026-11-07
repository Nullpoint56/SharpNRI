namespace SharpNRI.Native;

/// <summary>Expects "NRI_ENABLE_XLIB_SUPPORT"</summary>
public unsafe partial struct X11Window
{
    /// <summary>Display</summary>
    public void* dpy;

    /// <summary>Window</summary>
    [NativeTypeName("uint64_t")]
    public ulong window;
}
