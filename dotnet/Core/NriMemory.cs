// L2 — a memory allocation.
//
// NRI's memory model is Vulkan-shaped: create a resource, ask what memory it needs, allocate,
// bind. Memory is destroyed with FreeMemory, after everything bound to it.
//
// Freeing first is not an error NRI recovers from: with NRI validation on, FreeMemory reports the
// resources still bound and returns without freeing, so the allocation leaks while the wrapper
// would count it as gone, and on D3D12 every later D3D12CreateDevice in the process then fails with
// 0x887A0007 (AGENTS.md: Lifetime). So the memory counts what the binding bound into it, and
// refuses to be disposed while any of it lives, before NRI is called, as the device does for its
// children. The allocations of one AllocateAndBindMemory call share one count: NRI does not say
// which resource went into which, so every resource of the call holds all of them.

using SharpNRI.Native;
using Buffer = SharpNRI.Native.Buffer;

namespace SharpNRI;

/// <summary>A device memory allocation that buffers and textures are bound into.</summary>
/// <remarks>
/// <see cref="NriObject.Dispose"/> throws <see cref="InvalidOperationException"/>, and the memory
/// stays allocated and usable, while a resource bound into it or placed in it through the binding
/// is alive: <see cref="NriDevice.BindBufferMemory(ReadOnlySpan{NriBufferMemoryBinding})"/>,
/// <see cref="NriDevice.BindTextureMemory(ReadOnlySpan{NriTextureMemoryBinding})"/>,
/// <see cref="NriDevice.CreatePlacedBuffer"/>, <see cref="NriDevice.CreatePlacedTexture"/> and the
/// RayTracing extension's acceleration-structure and micromap binds and placements. Dispose those
/// first. The allocations <see cref="NriDevice.AllocateAndBindMemory"/> returns refuse while any
/// resource of that call is alive, each of them: NRI does not say which resource went into which.
/// </remarks>
public sealed unsafe class NriMemory : NriObject
{
    private readonly Holders _holders;

    internal NriMemory(NriDevice device, Memory* handle, Holders? group = null) : base(handle, "NriMemory")
    {
        Device = device;
        _holders = group ?? new Holders(group: false);
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    internal Memory* Raw => (Memory*)Handle;

    /// <summary>
    /// Resources bound into or placed in this memory through the binding and not yet disposed; for
    /// an allocation of <see cref="NriDevice.AllocateAndBindMemory"/>, those of the whole call.
    /// </summary>
    internal int BoundResources => Volatile.Read(ref _holders.Count);

    /// <summary>
    /// The live resources that keep one allocation, or every allocation of one
    /// <see cref="NriDevice.AllocateAndBindMemory"/> call, from being freed.
    /// </summary>
    internal sealed class Holders(bool group)
    {
        internal int Count;
        internal bool Group { get; } = group;
    }

    /// <summary>
    /// Records that the resource whose slot this is was bound into or placed in
    /// <paramref name="memory"/> (for a group allocation: into its call's allocations); the resource
    /// calls <see cref="Release"/> once it is destroyed.
    /// </summary>
    internal static void Track(ref Holders? slot, NriMemory memory)
    {
        Interlocked.Increment(ref memory._holders.Count);
        var previous = Interlocked.Exchange(ref slot, memory._holders);
        if (previous is not null) Interlocked.Decrement(ref previous.Count);
    }

    /// <summary>The resource whose slot this is was destroyed: it no longer holds its memory.</summary>
    internal static void Release(ref Holders? slot)
    {
        var holders = Interlocked.Exchange(ref slot, null);
        if (holders is not null) Interlocked.Decrement(ref holders.Count);
    }

    /// <summary>
    /// Refuses while resources bound into this memory are alive, before the handle is given up:
    /// freeing it first would leak it on every backend and, on D3D12, fail every later device creation.
    /// </summary>
    private protected override void BeforeDestroy()
    {
        var bound = Volatile.Read(ref _holders.Count);
        if (bound > 0 && _holders.Group)
        {
            throw new InvalidOperationException(
                $"NriMemory disposed with {bound} resource(s) of its AllocateAndBindMemory call still alive. NRI does not say which allocation holds which resource, so dispose every resource of the call before any of its allocations.");
        }
        if (bound > 0)
        {
            throw new InvalidOperationException(
                $"NriMemory disposed with {bound} resource(s) still bound to it. Dispose the buffers, textures, acceleration structures and micromaps bound into or placed in it first.");
        }
    }

    private protected override void Destroy(void* handle) => Device.Core.FreeMemory((Memory*)handle);
}

/// <summary>One buffer to bind into one memory at an offset. Bindings may overlap (aliasing).</summary>
public readonly unsafe record struct NriBufferMemoryBinding(NriBuffer Buffer, NriMemory Memory, ulong Offset = 0)
{
    internal BindBufferMemoryDesc ToNative() => new() { buffer = Buffer.Raw, memory = Memory.Raw, offset = Offset };
}

/// <summary>One texture to bind into one memory at an offset. Bindings may overlap (aliasing).</summary>
public readonly unsafe record struct NriTextureMemoryBinding(NriTexture Texture, NriMemory Memory, ulong Offset = 0)
{
    internal BindTextureMemoryDesc ToNative() => new() { texture = Texture.Raw, memory = Memory.Raw, offset = Offset };
}
