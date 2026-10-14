// L1 — lifetime for NRI objects.
//
// NRI has no reference counts. Every object is created by one call and destroyed by exactly one
// other, Destroy* is not thread safe, and the header says nothing about order beyond the obvious:
// a device must outlive everything created from it. That is an ownership hierarchy, and the GC
// finalizes in arbitrary order, so a finalizer that called Destroy* could destroy a buffer after
// its device — a use-after-free that presents as heap corruption, not a clean crash. PrismSlang
// learned this on its Linux leg; here it is a design decision from the first line.
//
// So: no finalizer. Each wrapper holds a managed reference to its device, so the device wrapper
// cannot be collected while a child exists; the device counts its live children and refuses to
// dispose while any remain, so a wrong disposal order is an exception rather than corruption. A
// forgotten Dispose leaks until process exit, which a profiler can find.
//
// Disposal after the runtime has started shutting down is skipped: the native library may already
// be unloaded, and a leak at exit costs nothing.

using PrismNRI.Native;

namespace PrismNRI;

/// <summary>Base of every wrapper over an NRI object. Disposing destroys the native object.</summary>
public abstract unsafe class NriObject : IDisposable
{
    private nint _handle;
    private readonly string _what;

    private static volatile bool s_processExiting;

    static NriObject()
    {
        AppDomain.CurrentDomain.ProcessExit += static (_, _) => s_processExiting = true;
        AppDomain.CurrentDomain.DomainUnload += static (_, _) => s_processExiting = true;
    }

    private protected NriObject(void* handle, string what)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _handle = (nint)handle;
        _what = what;
    }

    /// <summary>The device this object was created on. The device itself returns itself.</summary>
    public abstract NriDevice Device { get; }

    /// <summary>True once the native object has been destroyed.</summary>
    public bool IsDisposed => Volatile.Read(ref _handle) == 0;

    /// <summary>
    /// The live native pointer. Throws after disposal rather than handing out a dangling pointer.
    /// </summary>
    private protected void* Handle
    {
        get
        {
            var p = Volatile.Read(ref _handle);
            ObjectDisposedException.ThrowIf(p == 0, _what);
            return (void*)p;
        }
    }

    /// <summary>
    /// NRI's own handle for this object (<c>nri::Buffer*</c>, <c>nri::Device*</c>, ...), not the
    /// backend's, which is <c>GetNativeObject</c>. For calling <see cref="NriApi"/>, the
    /// <see cref="NriDevice.Core"/> and <see cref="NriDevice.Helper"/> tables, or an extension table
    /// acquired with <c>nriGetInterface</c>, where the binding has no managed form. Valid while this
    /// object lives; throws after disposal. The binding sees none of those calls: nothing here
    /// checks their arguments, and a native <c>Destroy*</c> through it leaves this wrapper
    /// dangling. NRI's validation still checks them when the device has it on. Last resort.
    /// </summary>
    public nint NriHandle => (nint)Handle;

    /// <summary>Calls the right <c>Destroy*</c> for this object. The pointer is still valid.</summary>
    private protected abstract void Destroy(void* handle);

    /// <summary>
    /// Runs before the handle is given up. Throwing here refuses the disposal and leaves the
    /// object alive and usable; the device uses it to refuse while children exist.
    /// </summary>
    private protected virtual void BeforeDestroy() { }

    /// <summary>
    /// Sets a debug name on the native object, shown by validation layers and GPU debuggers.
    /// D3D skips buffers and textures not yet bound to memory.
    /// </summary>
    public void SetDebugName(string name)
    {
        using var utf8 = new Interop.Utf8.Scoped(name);
        Device.Core.SetDebugName(Handle, utf8);
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _handle) == 0 || s_processExiting) return;
        BeforeDestroy();

        // Interlocked so concurrent Dispose calls cannot double-destroy.
        var p = Interlocked.Exchange(ref _handle, 0);
        if (p == 0) return;
        Destroy((void*)p);
        OnDestroyed();
    }

    /// <summary>Runs once after the native object is gone; the device uses it to count children.</summary>
    private protected virtual void OnDestroyed() => Device.ChildDestroyed();

    internal static byte Bool(bool value) => value ? (byte)1 : (byte)0;
}
