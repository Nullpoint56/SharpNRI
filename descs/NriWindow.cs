// Managed form of NRI's Window (NRISwapChain.h).
//
// NRI's Window is a struct, not a union: it holds one member per platform side by side, and the
// header says "only one entity must be initialized". The members hold native window-system
// pointers (HWND, Display*, wl_display*, CAMetalLayer*, ANativeWindow*), which a C# caller holds
// as nint; this form mirrors the struct member for member with nint for void*, so no unsafe code
// is needed to describe a window. Which member NRI reads is decided at NRI's compile time by
// platform (the validation layer checks the same one):
//
//   Windows   Windows.Hwnd
//   Linux     X11 (Dpy and Window, needs NRI_ENABLE_XLIB_SUPPORT) or Wayland (Display and
//             Surface, needs NRI_ENABLE_WAYLAND_SUPPORT)
//   Apple     Metal.CaMetalLayer
//   Android   Android.NativeWindow
//
// No device objects, so it lives in SharpNRI.Descs.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary><c>WindowsWindow</c>. Read on Windows.</summary>
/// <param name="Hwnd">The <c>HWND</c>.</param>
public readonly record struct NriWindowsWindow(nint Hwnd);

/// <summary><c>X11Window</c>. Read on Linux when NRI is built with <c>NRI_ENABLE_XLIB_SUPPORT</c>.</summary>
/// <param name="Dpy">The Xlib <c>Display*</c>.</param>
/// <param name="Window">The Xlib <c>Window</c> XID.</param>
public readonly record struct NriX11Window(nint Dpy, ulong Window);

/// <summary><c>WaylandWindow</c>. Read on Linux when NRI is built with <c>NRI_ENABLE_WAYLAND_SUPPORT</c>.</summary>
/// <param name="Display">The <c>wl_display*</c>.</param>
/// <param name="Surface">The <c>wl_surface*</c>.</param>
public readonly record struct NriWaylandWindow(nint Display, nint Surface);

/// <summary><c>MetalWindow</c>. Read on Apple platforms.</summary>
/// <param name="CaMetalLayer">The <c>CAMetalLayer*</c>.</param>
public readonly record struct NriMetalWindow(nint CaMetalLayer);

/// <summary><c>AndroidWindow</c>. Read on Android.</summary>
/// <param name="NativeWindow">The <c>ANativeWindow*</c>.</param>
public readonly record struct NriAndroidWindow(nint NativeWindow);

/// <summary>
/// <c>Window</c>: one member per platform, of which only the current platform's is read. Set
/// exactly one.
/// </summary>
public readonly record struct NriWindow
{
    public NriWindowsWindow Windows { get; init; }
    public NriX11Window X11 { get; init; }
    public NriWaylandWindow Wayland { get; init; }
    public NriMetalWindow Metal { get; init; }
    public NriAndroidWindow Android { get; init; }

    internal unsafe Window ToNative() => new()
    {
        windows = new WindowsWindow { hwnd = (void*)Windows.Hwnd },
        x11 = new X11Window { dpy = (void*)X11.Dpy, window = X11.Window },
        wayland = new WaylandWindow { display = (void*)Wayland.Display, surface = (void*)Wayland.Surface },
        metal = new MetalWindow { caMetalLayer = (void*)Metal.CaMetalLayer },
        android = new AndroidWindow { nativeWindow = (void*)Android.NativeWindow },
    };
}
