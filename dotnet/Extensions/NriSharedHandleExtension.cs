// L3 — the SharedHandle extension (patches/0002).
//
// Exporting a texture's memory and a fence to another API or process: a compositor importing the
// engine's viewport, an editor hosting the renderer. The extension is NRI-shaped and lives in the
// patch set because upstream NRI has no export path at all (docs/design.md §5).

using System.Runtime.InteropServices;
using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>
/// An exported handle. Owned by the caller: <see cref="Dispose"/> closes it (<c>CloseHandle</c>
/// on Windows, <c>close</c> on Linux). Importing it elsewhere usually duplicates it, so closing
/// after a successful import is the normal sequence.
/// </summary>
/// <param name="Handle">NT handle on Windows; file descriptor on Linux.</param>
/// <param name="Type">Which API and object kind the handle came from.</param>
/// <param name="Size">Vulkan: the allocation size, for the importer's <c>VkMemoryAllocateInfo</c>. 0 on D3D12.</param>
/// <param name="Offset">Vulkan: the texture's offset within the allocation, always 0 (dedicated).</param>
public readonly record struct NriSharedHandle(nint Handle, SharedHandleType Type, ulong Size, ulong Offset) : IDisposable
{
    public bool IsValid => Handle != 0 && Handle != -1;

    public void Dispose()
    {
        if (!IsValid) return;
        if (OperatingSystem.IsWindows()) CloseHandle(Handle);
        else close((int)Handle);
    }

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int CloseHandle(nint handle);

    [DllImport("libc", ExactSpelling = true)]
    private static extern int close(int fd);
}

/// <summary>NRI's <c>SharedHandleInterface</c>. Acquire through <see cref="NriDevice.SharedHandles"/>.</summary>
public sealed unsafe class NriSharedHandleExtension
{
    private readonly SharedHandleInterface _table;

    internal NriSharedHandleExtension(NriDevice device, in SharedHandleInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriSharedHandleExtension? extension)
    {
        if (InterfaceTable.TryAcquire<SharedHandleInterface>(device.Raw, "SharedHandleInterface", out var table))
        {
            extension = new NriSharedHandleExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// A committed texture with a dedicated, exportable allocation. Vulkan: opaque handle; D3D12:
    /// <c>D3D12_HEAP_FLAG_SHARED</c>. Everything else about it is an ordinary <see cref="NriTexture"/>.
    /// </summary>
    public NriTexture CreateSharedTexture(MemoryLocation location, in TextureDesc desc, float priority = 0)
    {
        Texture* handle = null;
        fixed (TextureDesc* p = &desc) Device.Check(_table.CreateSharedTexture(Device.Raw, location, priority, p, &handle), "CreateSharedTexture");
        return new NriTexture(Device, handle);
    }

    /// <summary>A timeline fence that can be exported.</summary>
    public NriFence CreateSharedFence(ulong initialValue = 0)
    {
        Fence* handle = null;
        Device.Check(_table.CreateSharedFence(Device.Raw, initialValue, &handle), "CreateSharedFence");
        return new NriFence(Device, handle);
    }

    /// <summary>
    /// A new handle to the texture's memory. The caller owns it. Throws with
    /// <see cref="Result.INVALID_ARGUMENT"/> for a texture not created by <see cref="CreateSharedTexture"/>.
    /// </summary>
    public NriSharedHandle GetSharedHandle(NriTexture texture)
    {
        SharedHandle h;
        Device.Check(_table.GetTextureSharedHandle(texture.Raw, &h), "GetTextureSharedHandle");
        return new NriSharedHandle((nint)h.handle, h.type, h.size, h.offset);
    }

    /// <summary>A new handle to the fence. The caller owns it.</summary>
    public NriSharedHandle GetSharedHandle(NriFence fence)
    {
        SharedHandle h;
        Device.Check(_table.GetFenceSharedHandle(fence.Raw, &h), "GetFenceSharedHandle");
        return new NriSharedHandle((nint)h.handle, h.type, h.size, h.offset);
    }
}
