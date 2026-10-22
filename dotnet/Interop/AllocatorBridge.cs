// L1 — host memory allocation, bridged to managed code.
//
// NRI allocates its own bookkeeping through AllocationCallbacks when they are supplied and
// forwards them into VMA and D3D12MA unless disable3rdPartyAllocationCallbacks is set. The
// callbacks must be thread safe: NRI's Create* calls are free-threaded.
//
// The default when no allocator is supplied is NRI's own, which is the right default for a
// consumer that does not care. A consumer that does care gets every byte NRI asks for, which is
// what makes the zero-allocation-per-call tests possible: NriCountingAllocator is the instrument.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>
/// A host allocator NRI can use for its own bookkeeping. Must be thread safe. Alignment is a
/// power of two and must be honoured. Must not fail: a null (or an exception, which the binding
/// turns into one) fails the creation of an NRI object with <see cref="Result.OUT_OF_MEMORY"/>
/// (patch 0034), but NRI's containers allocate here too and cannot survive one.
/// </summary>
public interface INriAllocator
{
    unsafe void* Allocate(nuint size, nuint alignment);

    /// <summary>
    /// Resizes <paramref name="memory"/>, keeping its contents up to the smaller size. Reached
    /// through Vulkan's host allocation callbacks, which NRI routes here with
    /// <c>ForwardAllocatorToThirdParty</c>, so their contract applies: a null
    /// <paramref name="memory"/> allocates, and a <paramref name="size"/> of 0 frees
    /// <paramref name="memory"/> and returns null.
    /// </summary>
    unsafe void* Reallocate(void* memory, nuint size, nuint alignment);

    unsafe void Free(void* memory);
}

/// <summary>
/// An <see cref="INriAllocator"/> over <see cref="NativeMemory"/> that counts what it hands out.
/// The counters are the instrument the allocation tests read; they are also what an application reads
/// to know how much NRI is allocating on its behalf.
/// </summary>
public sealed unsafe class NriCountingAllocator : INriAllocator
{
    private long _allocations;
    private long _reallocations;
    private long _frees;

    /// <summary>Blocks handed out so far: <see cref="Allocate"/> calls, and <see cref="Reallocate"/> calls with no block.</summary>
    public long Allocations => Volatile.Read(ref _allocations);

    /// <summary><see cref="Reallocate"/> calls that resized a block.</summary>
    public long Reallocations => Volatile.Read(ref _reallocations);

    /// <summary>Blocks freed so far: <see cref="Free"/> calls with a block, and <see cref="Reallocate"/> calls to size 0.</summary>
    public long Frees => Volatile.Read(ref _frees);

    /// <summary>Allocations that have not been freed.</summary>
    public long Live => Allocations - Frees;

    public void* Allocate(nuint size, nuint alignment)
    {
        Interlocked.Increment(ref _allocations);
        return NativeMemory.AlignedAlloc(size == 0 ? 1 : size, alignment == 0 ? (nuint)sizeof(void*) : alignment);
    }

    /// <summary>
    /// Resizes <paramref name="memory"/>. With no block it allocates (counted in
    /// <see cref="Allocations"/>); to size 0 it frees the block and returns null (counted in
    /// <see cref="Frees"/>), as Vulkan's reallocation callback contract and NRI's own Windows
    /// default (<c>_aligned_realloc</c>) define it.
    /// </summary>
    public void* Reallocate(void* memory, nuint size, nuint alignment)
    {
        if (memory is null) return Allocate(size, alignment);
        if (size == 0)
        {
            Free(memory);
            return null;
        }
        Interlocked.Increment(ref _reallocations);
        return NativeMemory.AlignedRealloc(memory, size, alignment == 0 ? (nuint)sizeof(void*) : alignment);
    }

    public void Free(void* memory)
    {
        if (memory is null) return;
        Interlocked.Increment(ref _frees);
        NativeMemory.AlignedFree(memory);
    }
}

internal sealed unsafe class AllocatorBridge : IDisposable
{
    private readonly INriAllocator _allocator;
    private readonly bool _forwardToThirdParty;
    private GCHandle _self;

    internal AllocatorBridge(INriAllocator allocator, bool forwardToThirdParty)
    {
        _allocator = allocator;
        _forwardToThirdParty = forwardToThirdParty;
        _self = GCHandle.Alloc(this, GCHandleType.Normal);
    }

    internal AllocationCallbacks Native => new()
    {
        Allocate = &OnAllocate,
        Reallocate = &OnReallocate,
        Free = &OnFree,
        userArg = (void*)GCHandle.ToIntPtr(_self),
        disable3rdPartyAllocationCallbacks = _forwardToThirdParty ? (byte)0 : (byte)1,
    };

    public void Dispose()
    {
        if (_self.IsAllocated) _self.Free();
    }

    private static INriAllocator? FromUserArg(void* userArg)
    {
        if (userArg is null) return null;
        var handle = GCHandle.FromIntPtr((nint)userArg);
        return handle.IsAllocated ? (handle.Target as AllocatorBridge)?._allocator : null;
    }

    // Null on any failure. For an NRI object the creation call then fails with OUT_OF_MEMORY (patch
    // 0034); NRI's containers allocate through the same callbacks and cannot report it, so a null
    // there is an access violation. The alternative, an exception crossing a native frame, is a
    // process abort on Linux.

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void* OnAllocate(void* userArg, nuint size, nuint alignment)
    {
        try
        {
            var allocator = FromUserArg(userArg);
            return allocator is null ? null : allocator.Allocate(size, alignment);
        }
        catch { return null; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void* OnReallocate(void* userArg, void* memory, nuint size, nuint alignment)
    {
        try
        {
            var allocator = FromUserArg(userArg);
            return allocator is null ? null : allocator.Reallocate(memory, size, alignment);
        }
        catch { return null; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnFree(void* userArg, void* memory)
    {
        try { FromUserArg(userArg)?.Free(memory); }
        catch { }
    }
}
