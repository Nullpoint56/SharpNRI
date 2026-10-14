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
/// power of two and must be honoured.
/// </summary>
public interface INriAllocator
{
    unsafe void* Allocate(nuint size, nuint alignment);
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

    /// <summary>Calls to <see cref="Allocate"/> so far.</summary>
    public long Allocations => Volatile.Read(ref _allocations);

    /// <summary>Calls to <see cref="Reallocate"/> so far.</summary>
    public long Reallocations => Volatile.Read(ref _reallocations);

    /// <summary>Calls to <see cref="Free"/> so far.</summary>
    public long Frees => Volatile.Read(ref _frees);

    /// <summary>Allocations that have not been freed.</summary>
    public long Live => Allocations - Frees;

    public void* Allocate(nuint size, nuint alignment)
    {
        Interlocked.Increment(ref _allocations);
        return NativeMemory.AlignedAlloc(size == 0 ? 1 : size, alignment == 0 ? (nuint)sizeof(void*) : alignment);
    }

    public void* Reallocate(void* memory, nuint size, nuint alignment)
    {
        if (memory is null) return Allocate(size, alignment);
        Interlocked.Increment(ref _reallocations);
        return NativeMemory.AlignedRealloc(memory, size == 0 ? 1 : size, alignment == 0 ? (nuint)sizeof(void*) : alignment);
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

    // Null on any failure, which NRI treats as out-of-memory and reports as such. The alternative,
    // an exception crossing a native frame, is a process abort on Linux.

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
