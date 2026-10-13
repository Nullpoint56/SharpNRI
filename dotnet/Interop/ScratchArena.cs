// L1 — per-thread native scratch memory for marshalling deep descs.
//
// Most per-call arrays fit a stackalloc in the method that calls NRI. The Video extension's descs
// do not: they nest optional structs and arrays three levels deep (a decode desc points at an AV1
// picture desc that points at a tile layout that points at four arrays), and a stackalloc cannot
// be returned from the helper that marshals one level. This arena is the alternative: a bump
// allocator over native chunks, one per thread, reset after each call. Steady state allocates
// nothing, managed or native; a call that needs more than the chunks hold adds a chunk once.
//
// Nothing handed out may outlive the Return that follows the NRI call: NRI copies what it keeps.

using System.Runtime.InteropServices;

namespace PrismNRI.Interop;

internal sealed unsafe class ScratchArena
{
    private const int Alignment = 16;
    private const int InitialChunk = 16 * 1024;

    [ThreadStatic] private static ScratchArena? t_free;

    private readonly List<(nint Pointer, nuint Size)> _chunks = [];
    private int _chunk;
    private nuint _offset;

    /// <summary>The calling thread's arena, or a new one when it is already in use (re-entrancy).</summary>
    internal static ScratchArena Rent()
    {
        var arena = t_free ?? new ScratchArena();
        t_free = null;
        return arena;
    }

    /// <summary>Releases everything handed out since <see cref="Rent"/> and makes the arena available again.</summary>
    internal void Return()
    {
        _chunk = 0;
        _offset = 0;
        t_free = this;
    }

    /// <summary><paramref name="count"/> zeroed elements; null for 0.</summary>
    internal T* Alloc<T>(int count) where T : unmanaged
    {
        if (count <= 0) return null;
        var size = (nuint)count * (nuint)sizeof(T);
        var p = (T*)Bump(size);
        NativeMemory.Clear(p, size);
        return p;
    }

    /// <summary>A copy of <paramref name="source"/>; null when it is empty.</summary>
    internal T* Copy<T>(ReadOnlySpan<T> source) where T : unmanaged
    {
        if (source.IsEmpty) return null;
        var p = (T*)Bump((nuint)source.Length * (nuint)sizeof(T));
        source.CopyTo(new Span<T>(p, source.Length));
        return p;
    }

    /// <summary>A copy of one value.</summary>
    internal T* Push<T>(in T value) where T : unmanaged
    {
        var p = (T*)Bump((nuint)sizeof(T));
        *p = value;
        return p;
    }

    private void* Bump(nuint size)
    {
        size = (size + Alignment - 1) & ~(nuint)(Alignment - 1);
        while (true)
        {
            if (_chunk < _chunks.Count)
            {
                var (pointer, capacity) = _chunks[_chunk];
                if (_offset + size <= capacity)
                {
                    var p = (byte*)pointer + _offset;
                    _offset += size;
                    return p;
                }
                _chunk++;
                _offset = 0;
                continue;
            }

            var last = _chunks.Count == 0 ? 0 : _chunks[^1].Size;
            var chunkSize = Math.Max(Math.Max((nuint)InitialChunk, last * 2), size);
            _chunks.Add(((nint)NativeMemory.AlignedAlloc(chunkSize, Alignment), chunkSize));
        }
    }
}
