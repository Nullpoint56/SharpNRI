// L1 — acquiring an NRI function table.
//
// NRI's API is a set of structs of function pointers. nriGetInterface fills one by name, and it is
// also NRI's own ABI check: the implementation compares the size the caller passes against the
// size it compiled with and refuses a mismatch with INVALID_ARGUMENT after reporting the two
// numbers through the message callback (Creation.cpp). A generated table that gained or lost a
// slot against the pinned header therefore fails here, loudly, before any call through it.
//
// UNSUPPORTED means the backend does not implement the extension on this device (ray tracing on a
// device without it, the D3D12 wrapper on a Vulkan device). That is a capability answer, not a
// bug, and is surfaced as NriUnsupportedException so a caller can probe.

using SharpNRI.Native;

namespace SharpNRI.Interop;

internal static unsafe class InterfaceTable
{
    /// <summary>
    /// Fills a table of type <typeparamref name="T"/> for <paramref name="device"/>.
    /// <paramref name="name"/> is the struct's name in the header, e.g. <c>CoreInterface</c>.
    /// </summary>
    internal static T Acquire<T>(Device* device, string name) where T : unmanaged
    {
        T table = default;
        using var utf8 = new Utf8.Scoped(name);
        var result = NriApi.nriGetInterface(device, utf8, (nuint)sizeof(T), &table);
        NriException.Check(result, $"nriGetInterface({name})");
        return table;
    }

    /// <summary>
    /// Like <see cref="Acquire{T}"/> but answers false for <see cref="Result.UNSUPPORTED"/> instead
    /// of throwing. Any other failure still throws.
    /// </summary>
    internal static bool TryAcquire<T>(Device* device, string name, out T table) where T : unmanaged
    {
        T t = default;
        using var utf8 = new Utf8.Scoped(name);
        var result = NriApi.nriGetInterface(device, utf8, (nuint)sizeof(T), &t);
        table = t;
        if (result == Result.UNSUPPORTED) return false;
        NriException.Check(result, $"nriGetInterface({name})");
        return true;
    }
}
