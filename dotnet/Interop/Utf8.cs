// L1 — UTF-8 across the boundary.
//
// NRI takes `const char*` for debug names, annotations, interface names and extension names, and
// returns it from nriGetGraphicsAPIString and FormatProps.name. Nothing in the generated layer
// converts; this does.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpNRI.Interop;

internal static unsafe class Utf8
{
    /// <summary>
    /// A NUL-terminated UTF-8 copy in unmanaged memory; null in, null out. Free with <see cref="Free"/>.
    /// </summary>
    internal static sbyte* Alloc(string? value)
    {
        if (value is null) return null;
        var count = Encoding.UTF8.GetByteCount(value);
        var p = (byte*)NativeMemory.Alloc((nuint)count + 1);
        fixed (char* src = value)
        {
            Encoding.UTF8.GetBytes(src, value.Length, p, count);
        }
        p[count] = 0;
        return (sbyte*)p;
    }

    internal static void Free(sbyte* p)
    {
        if (p is not null) NativeMemory.Free(p);
    }

    /// <summary>Reads a NUL-terminated UTF-8 string NRI owns. Null in, null out.</summary>
    internal static string? Read(sbyte* p) => p is null ? null : Marshal.PtrToStringUTF8((nint)p);

    /// <summary>Reads a NUL-terminated string out of a fixed-size inline char buffer.</summary>
    internal static string ReadFixed(ReadOnlySpan<sbyte> buffer)
    {
        var bytes = MemoryMarshal.AsBytes(buffer);
        var end = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    }

    /// <summary>Stack bytes a per-frame string (an annotation) is encoded into before native memory is used.</summary>
    internal const int StackBytes = 256;

    /// <summary>Scoped allocation so call sites read as C# rather than try/finally ladders.</summary>
    internal readonly ref struct Scoped
    {
        private readonly sbyte* _p;
        private readonly bool _owned;

        internal Scoped(string? value)
        {
            _p = Alloc(value);
            _owned = true;
        }

        /// <summary>
        /// Encodes into <paramref name="stack"/>, which must be stack memory (a <c>stackalloc</c>),
        /// when the text and its NUL fit, and into native memory otherwise, so a per-frame string
        /// allocates nothing.
        /// </summary>
        internal Scoped(string? value, Span<byte> stack)
        {
            if (value is null || (Encoding.UTF8.GetMaxByteCount(value.Length) >= stack.Length && Encoding.UTF8.GetByteCount(value) >= stack.Length))
            {
                _p = Alloc(value);
                _owned = true;
                return;
            }
            var count = Encoding.UTF8.GetBytes(value, stack);
            stack[count] = 0;
            _p = (sbyte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(stack));
        }

        public static implicit operator sbyte*(Scoped s) => s._p;

        public void Dispose()
        {
            if (_owned) Free(_p);
        }
    }

    /// <summary>
    /// A NUL-terminated array of NUL-terminated strings, as <c>const char* const*</c> wants.
    /// </summary>
    internal sealed class StringArray : IDisposable
    {
        private sbyte** _items;
        public uint Count { get; }

        public StringArray(IReadOnlyList<string> values)
        {
            Count = (uint)values.Count;
            _items = (sbyte**)NativeMemory.Alloc((nuint)(values.Count + 1), (nuint)sizeof(sbyte*));
            for (var i = 0; i < values.Count; i++) _items[i] = Alloc(values[i]);
            _items[values.Count] = null;
        }

        public sbyte** Pointer => _items;

        public void Dispose()
        {
            if (_items is null) return;
            for (var i = 0; i < Count; i++) Free(_items[i]);
            NativeMemory.Free(_items);
            _items = null;
        }
    }
}
