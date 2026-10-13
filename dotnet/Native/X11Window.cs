namespace PrismNRI.Native;

public unsafe partial struct X11Window
{
    public void* dpy;

    [NativeTypeName("uint64_t")]
    public ulong window;
}
