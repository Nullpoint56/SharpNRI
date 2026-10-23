// L2 — an exported handle (patch 0002: BufferDesc::shared, TextureDesc::shared, FenceDesc::shared
// and the Core getters GetBufferSharedHandle / GetTextureSharedHandle / GetFenceSharedHandle).
//
// Exporting a buffer's or texture's memory and a fence to another API, device or process. The
// fields and getters live in the patch set because upstream NRI has no export path at all
// (CLAUDE.md: Patches, Shared handles).

using System.Runtime.InteropServices;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>
/// An exported handle. Owned by the caller: <see cref="Dispose"/> closes it (<c>CloseHandle</c>
/// on Windows, <c>close</c> on Linux). Importing it elsewhere usually duplicates it, so closing
/// after a successful import is the normal sequence.
/// </summary>
/// <remarks>
/// A class, so that every reference shares one state: the first <see cref="Dispose"/> takes the
/// value with an atomic exchange and closes it, and every later one, from any thread, finds 0 and
/// does nothing. A second close could hit a handle the process has reused the value for. No
/// finalizer, as nowhere in the binding: a handle never disposed stays open until the process exits.
/// </remarks>
public sealed class NriSharedHandle : IDisposable
{
    private nint _handle;

    /// <summary>Takes ownership of <paramref name="handle"/>: <see cref="Dispose"/> closes it.</summary>
    /// <param name="handle">NT handle on Windows; file descriptor on Linux.</param>
    /// <param name="type">Which API and object kind the handle came from.</param>
    /// <param name="size">Resources: the allocation size, for the importer (<c>VkMemoryAllocateInfo::allocationSize</c>, <c>CUDA_EXTERNAL_MEMORY_HANDLE_DESC::size</c>). 0 for a fence.</param>
    public NriSharedHandle(nint handle, SharedHandleType type, ulong size)
    {
        _handle = handle;
        Type = type;
        Size = size;
    }

    internal static unsafe NriSharedHandle From(in SharedHandle h) => new((nint)h.handle, h.type, h.size);

    /// <summary>NT handle on Windows; file descriptor on Linux. 0 once disposed.</summary>
    public nint Handle => Volatile.Read(ref _handle);

    /// <summary>Which API and object kind the handle came from.</summary>
    public SharedHandleType Type { get; }

    /// <summary>Resources: the allocation size, for the importer. 0 for a fence.</summary>
    public ulong Size { get; }

    /// <summary>False for 0 and -1, and once disposed.</summary>
    public bool IsValid => Handle is not 0 and not -1;

    /// <summary>Closes the handle the first time; does nothing after that, or for an invalid handle.</summary>
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle is 0 or -1) return;
        if (OperatingSystem.IsWindows()) CloseHandle(handle);
        else close((int)handle);
    }

    public override string ToString() =>
        $"NriSharedHandle {{ Handle = {Handle}, Type = {Type}, Size = {Size}, IsValid = {IsValid} }}";

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int CloseHandle(nint handle);

    [DllImport("libc", ExactSpelling = true)]
    private static extern int close(int fd);
}
