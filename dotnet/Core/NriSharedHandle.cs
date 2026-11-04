// L2 — an exported handle (NRI #294, eb4ed356: BufferDesc::shared, TextureDesc::shared,
// FenceDesc::shared, the Core getters GetBufferSharedHandle / GetTextureSharedHandle /
// GetFenceSharedHandle, and f18c6df9's DestroySharedHandle).
//
// Exporting a buffer's or texture's memory and a fence to another API, device or process
// (AGENTS.md: Shared handles).

using SharpNRI.Native;

namespace SharpNRI;

/// <summary>
/// An exported handle, owned by the caller: <see cref="Dispose"/> hands it to NRI's
/// <c>DestroySharedHandle</c>, which closes it (<c>CloseHandle</c> for an NT handle,
/// <c>close</c> for a file descriptor). Importing an NT handle elsewhere duplicates it, so
/// disposing after a successful import is the normal sequence. An import that takes ownership (a
/// Vulkan or CUDA import of a file descriptor) closes it itself: give it up with
/// <see cref="Detach"/> instead, as NRI's header says ("After an FD import takes ownership, clear
/// SharedHandle instead of destroying it").
/// </summary>
/// <remarks>
/// A class, so that every reference shares one state: the first <see cref="Dispose"/> or
/// <see cref="Detach"/> takes the handle with an atomic exchange, and every later
/// <see cref="Dispose"/>, from any thread, does nothing. A second close could hit a handle the
/// process has reused the value for. Once released the object reads as NRI's cleared struct:
/// <see cref="Handle"/> 0, <see cref="Type"/> <see cref="SharedHandleType.NONE"/>, <see cref="Size"/> 0.
/// No finalizer, as nowhere in the binding: a handle never disposed stays open until the process
/// exits. <c>DestroySharedHandle</c> takes no device, so the handle may outlive the device that
/// exported it.
/// </remarks>
public sealed unsafe class NriSharedHandle : IDisposable
{
    private readonly delegate* unmanaged[Stdcall]<SharedHandle*, void> _destroy;
    private readonly nint _handle;
    private readonly SharedHandleType _type;
    private readonly ulong _size;
    private int _released;

    /// <summary>
    /// Takes ownership of <paramref name="handle"/>: <see cref="Dispose"/> closes it through
    /// <paramref name="device"/>'s <c>DestroySharedHandle</c>.
    /// </summary>
    /// <param name="device">The device whose Core table closes the handle.</param>
    /// <param name="handle">NT handle on Windows; file descriptor on Linux (0 is a valid descriptor).</param>
    /// <param name="type">Which API and object kind the handle came from; <see cref="SharedHandleType.NONE"/> is empty.</param>
    /// <param name="size">Resources: the allocation size, for the importer (<c>VkMemoryAllocateInfo::allocationSize</c>, <c>CUDA_EXTERNAL_MEMORY_HANDLE_DESC::size</c>). 0 for a fence.</param>
    public NriSharedHandle(NriDevice device, nint handle, SharedHandleType type, ulong size)
    {
        ArgumentNullException.ThrowIfNull(device);
        _destroy = device.Core.DestroySharedHandle;
        _handle = handle;
        _type = type;
        _size = size;
    }

    internal static NriSharedHandle From(NriDevice device, in SharedHandle h) => new(device, (nint)h.handle, h.type, h.size);

    private bool Released => Volatile.Read(ref _released) != 0;

    /// <summary>NT handle on Windows; file descriptor on Linux (0 is a valid descriptor). 0 once disposed or detached.</summary>
    public nint Handle => Released ? 0 : _handle;

    /// <summary>Which API and object kind the handle came from. <see cref="SharedHandleType.NONE"/> once disposed or detached.</summary>
    public SharedHandleType Type => Released ? SharedHandleType.NONE : _type;

    /// <summary>Resources: the allocation size, for the importer. 0 for a fence, and once disposed or detached.</summary>
    public ulong Size => Released ? 0 : _size;

    /// <summary>
    /// False once disposed or detached, for <see cref="SharedHandleType.NONE"/> (what NONE exports),
    /// for -1, and for 0 unless the handle is a file descriptor.
    /// </summary>
    public bool IsValid => !Released && IsValidValue(_handle, _type);

    private static bool IsValidValue(nint handle, SharedHandleType type) =>
        type != SharedHandleType.NONE && handle != -1 && (handle != 0 || type == SharedHandleType.OPAQUE_FD);

    /// <summary>
    /// Closes the handle through NRI's <c>DestroySharedHandle</c> the first time; does nothing after
    /// that, after <see cref="Detach"/>, or for a handle that is not valid.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0 || !IsValidValue(_handle, _type)) return;
        var h = new SharedHandle { handle = (void*)_handle, type = _type, size = _size };
        _destroy(&h);
    }

    /// <summary>
    /// Gives the handle up without closing it and returns it: for an import that takes ownership (a
    /// file descriptor imported into Vulkan or CUDA is closed by the importer). The object then
    /// reads as disposed and <see cref="Dispose"/> does nothing. Throws
    /// <see cref="ObjectDisposedException"/> when already disposed or detached.
    /// </summary>
    public nint Detach()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            throw new ObjectDisposedException(nameof(NriSharedHandle), "The shared handle was already disposed or detached.");
        return _handle;
    }

    public override string ToString() =>
        $"NriSharedHandle {{ Handle = {Handle}, Type = {Type}, Size = {Size}, IsValid = {IsValid} }}";
}
