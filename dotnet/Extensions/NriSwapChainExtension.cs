// L3 — the SwapChain extension (NRISwapChain.h).
//
// Presentation. The swap chain is an object the extension creates and destroys; its textures are
// created by NRI with it and destroyed with it, so they come back as non-owning NriTexture wrappers
// (not device children, Dispose a no-op) that the swap chain invalidates when it is disposed.
// Available when DeviceDesc.features.swapChain is set; UNSUPPORTED otherwise. Threadsafe per NRI.
//
// Semaphores: "acquireSemaphore" and "releaseSemaphore" are fences created with the
// SWAPCHAIN_SEMAPHORE initial value (NriDevice.CreateFence(NriSwapChainExtension.SwapChainSemaphore));
// D3D ignores them. The frame loop in the header comment is the reference for how they cycle.

using SharpNRI.Interop;
using SharpNRI.Native;

namespace SharpNRI;

/// <summary><c>SwapChainDesc</c> with the window as <see cref="NriWindow"/> and the queue as a wrapper.</summary>
public sealed class NriSwapChainDesc
{
    /// <summary>The window to present to; only the current platform's member is read.</summary>
    public required NriWindow Window { get; init; }

    /// <summary>GRAPHICS, or COMPUTE with <c>features.presentFromCompute</c>.</summary>
    public required NriQueue Queue { get; init; }

    public required ushort Width { get; init; }
    public required ushort Height { get; init; }

    /// <summary>Desired count; the real one is <see cref="NriSwapChain.Textures"/>.Count.</summary>
    public required byte TextureNum { get; init; }

    /// <summary>Desired format; the real one is the <c>TextureDesc</c> of a swap chain texture.</summary>
    public SwapChainFormat Format { get; init; }

    /// <summary>
    /// <c>LATEST_READY</c> with <c>VSYNC</c> asks for FIFO_LATEST_READY on Vulkan (vblank-paced, the
    /// newest ready image, no tearing) when <c>features.latestReadySwapChain</c> is set, and is
    /// ignored otherwise (FIFO, or FIFO_RELAXED with <c>ALLOW_TEARING</c>). NRI never reports the
    /// present mode it chose.
    /// </summary>
    public SwapChainBits Flags { get; init; }

    /// <summary>Optional, "max frame latency". 0 auto-selects 1 (waitable) or 2.</summary>
    public byte QueuedFrameNum { get; init; }

    /// <summary>
    /// Optional. Silently ignored without <c>features.resizableSwapChain</c> or when the
    /// implicitly chosen present mode does not support it. VK: unsupported scaling triggers
    /// <c>OUT_OF_DATE</c> on resizing.
    /// </summary>
    public Scaling Scaling { get; init; }

    public Gravity GravityX { get; init; }
    public Gravity GravityY { get; init; }
}

/// <summary>A swap chain. Created by <see cref="NriSwapChainExtension.CreateSwapChain"/>.</summary>
public sealed unsafe class NriSwapChain : NriObject
{
    private readonly NriSwapChainExtension _extension;
    private readonly NriTexture[] _textures;

    internal NriSwapChain(NriSwapChainExtension extension, SwapChain* handle, NriQueue queue) : base(handle, "NriSwapChain")
    {
        _extension = extension;
        Queue = queue;
        extension.Device.ChildCreated();

        uint count = 0;
        var textures = extension.GetSwapChainTexturesRaw(handle, &count);
        _textures = new NriTexture[count];
        for (var i = 0; i < count; i++) _textures[i] = new NriTexture(extension.Device, textures[i], owned: false);
    }

    public override NriDevice Device => _extension.Device;

    /// <summary>The queue the swap chain presents on.</summary>
    public NriQueue Queue { get; }

    /// <summary>
    /// The swap chain's textures (<c>GetSwapChainTextures</c>), created as colour attachments.
    /// Owned by the swap chain: disposing one is a no-op, and all become disposed with it.
    /// </summary>
    public IReadOnlyList<NriTexture> Textures => _textures;

    internal SwapChain* Raw => (SwapChain*)Handle;

    private protected override void Destroy(void* handle) => _extension.DestroySwapChain((SwapChain*)handle);

    private protected override void OnDestroyed()
    {
        foreach (var t in _textures) t.Dispose();
        base.OnDestroyed();
    }
}

/// <summary>NRI's <c>SwapChainInterface</c>. Acquire through <see cref="NriDevice.SwapChain"/>.</summary>
public sealed unsafe class NriSwapChainExtension
{
    /// <summary>
    /// <c>SWAPCHAIN_SEMAPHORE</c>: the special <c>initialValue</c> for <see cref="NriDevice.CreateFence"/>
    /// that makes a fence usable as an acquire or release semaphore.
    /// </summary>
    public const ulong SwapChainSemaphore = NriApi.SWAPCHAIN_SEMAPHORE;

    private readonly SwapChainInterface _table;

    internal NriSwapChainExtension(NriDevice device, in SwapChainInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriSwapChainExtension? extension)
    {
        if (InterfaceTable.TryAcquire<SwapChainInterface>(device.Raw, "SwapChainInterface", out var table))
        {
            extension = new NriSwapChainExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// A swap chain for a window. Its textures are created as colour attachments; query the real
    /// count, size and format from <see cref="NriSwapChain.Textures"/>. Vulkan clamps the size to
    /// what the surface allows (the window's current size without present scaling) and answers
    /// <c>NOT_READY</c> for a window with no drawable area, typically a minimised one, which this
    /// method throws as <see cref="NriException"/>; <see cref="TryCreateSwapChain"/> returns it.
    /// </summary>
    public NriSwapChain CreateSwapChain(NriSwapChainDesc swapChainDesc)
    {
        var desc = ToNative(swapChainDesc);
        SwapChain* handle = null;
        Device.Check(_table.CreateSwapChain(Device.Raw, &desc, &handle), "CreateSwapChain");
        return new NriSwapChain(this, handle, swapChainDesc.Queue);
    }

    /// <summary>
    /// <see cref="CreateSwapChain"/> for a window that may have no drawable area: returns
    /// <see cref="NriSwapChainStatus.NotReady"/> with no swap chain instead of throwing when Vulkan
    /// answers <c>NOT_READY</c> (zero surface extent). Process window events and retry. Every
    /// other failure throws.
    /// </summary>
    public NriSwapChainStatus TryCreateSwapChain(NriSwapChainDesc swapChainDesc, out NriSwapChain? swapChain)
    {
        var desc = ToNative(swapChainDesc);
        SwapChain* handle = null;
        var result = _table.CreateSwapChain(Device.Raw, &desc, &handle);
        if (result == Result.NOT_READY)
        {
            swapChain = null;
            return NriSwapChainStatus.NotReady;
        }
        Device.Check(result, "CreateSwapChain");
        swapChain = new NriSwapChain(this, handle, swapChainDesc.Queue);
        return NriSwapChainStatus.Ok;
    }

    private static SwapChainDesc ToNative(NriSwapChainDesc swapChainDesc)
    {
        ArgumentNullException.ThrowIfNull(swapChainDesc);
        return new SwapChainDesc
        {
            window = swapChainDesc.Window.ToNative(),
            queue = swapChainDesc.Queue.Raw,
            width = swapChainDesc.Width,
            height = swapChainDesc.Height,
            textureNum = swapChainDesc.TextureNum,
            format = swapChainDesc.Format,
            flags = swapChainDesc.Flags,
            queuedFrameNum = swapChainDesc.QueuedFrameNum,
            scaling = swapChainDesc.Scaling,
            gravityX = swapChainDesc.GravityX,
            gravityY = swapChainDesc.GravityY,
        };
    }

    internal void DestroySwapChain(SwapChain* swapChain) => _table.DestroySwapChain(swapChain);

    internal Texture** GetSwapChainTexturesRaw(SwapChain* swapChain, uint* textureNum) => _table.GetSwapChainTextures(swapChain, textureNum);

    /// <summary><c>GetSwapChainTextures</c>: the swap chain's textures, owned by it. Same as <see cref="NriSwapChain.Textures"/>.</summary>
    public IReadOnlyList<NriTexture> GetSwapChainTextures(NriSwapChain swapChain) => swapChain.Textures;

    /// <summary>
    /// Colour settings and capabilities of the display closest to the swap chain's window.
    /// Throws with <see cref="Result.FAILURE"/> when the window is outside every monitor.
    /// </summary>
    public DisplayDesc GetDisplayDesc(NriSwapChain swapChain)
    {
        DisplayDesc desc;
        Device.Check(_table.GetDisplayDesc(swapChain.Raw, &desc), "GetDisplayDesc");
        return desc;
    }

    /// <summary>
    /// Acquires the next texture to render to into <paramref name="textureIndex"/>.
    /// <paramref name="acquireSemaphore"/> must be created with <see cref="SwapChainSemaphore"/>
    /// (ignored in D3D). <see cref="NriSwapChainStatus.OutOfDate"/> (VK only: the window changed
    /// and the swap chain no longer matches it): nothing was acquired, the semaphore is not
    /// signalled; recreate the swap chain. NRI reports no error for it. Any other failure throws.
    /// </summary>
    public NriSwapChainStatus AcquireNextTexture(NriSwapChain swapChain, NriFence acquireSemaphore, out uint textureIndex)
    {
        uint index = 0;
        var result = _table.AcquireNextTexture(swapChain.Raw, acquireSemaphore.Raw, &index);
        textureIndex = index;
        if (result == Result.OUT_OF_DATE) return NriSwapChainStatus.OutOfDate;
        Device.Check(result, "AcquireNextTexture");
        return NriSwapChainStatus.Ok;
    }

    /// <summary>
    /// Waits until the presentation identified by <paramref name="presentId"/> (a previously
    /// queued one) is shown. Call once immediately before input sampling. Requires the swap chain
    /// to be created <c>WAITABLE</c> (<c>features.waitableSwapChain</c>).
    /// <paramref name="presentId"/> must be the non-zero id of a tracked present, one passed to
    /// <see cref="QueuePresent"/>: NRI validation refuses 0 with <c>INVALID_ARGUMENT</c> and an
    /// ERROR message on every backend, also on D3D12, where the id is ignored and the wait is the
    /// swap chain's frame-latency waitable object. Vulkan's <c>vkWaitForPresentKHR</c> needs the
    /// id of a tracked present, so the rule is the portable one. Pass the id of the last present
    /// queued with a non-zero id (AGENTS.md: Extensions).
    /// </summary>
    public void WaitForPresent(NriSwapChain swapChain, ulong presentId) =>
        Device.Check(_table.WaitForPresent(swapChain.Raw, presentId), "WaitForPresent");

    /// <summary>
    /// Presents the acquired texture. A non-zero <paramref name="presentId"/> tracks the frame
    /// and must strictly increase per swap chain (gaps allowed); 0 is untracked, and an untracked
    /// present cannot be waited for with <see cref="WaitForPresent"/>.
    /// <paramref name="releaseSemaphore"/> must be created with <see cref="SwapChainSemaphore"/>
    /// (ignored in D3D). <see cref="NriSwapChainStatus.OutOfDate"/> (VK only): the wait on
    /// <paramref name="releaseSemaphore"/> still happens, the image is not shown; recreate the
    /// swap chain. NRI reports no error for it. Any other failure throws.
    /// </summary>
    public NriSwapChainStatus QueuePresent(NriSwapChain swapChain, NriFence releaseSemaphore, ulong presentId = 0)
    {
        var result = _table.QueuePresent(swapChain.Raw, releaseSemaphore.Raw, presentId);
        if (result == Result.OUT_OF_DATE) return NriSwapChainStatus.OutOfDate;
        Device.Check(result, "QueuePresent");
        return NriSwapChainStatus.Ok;
    }
}

/// <summary>
/// What <see cref="NriSwapChainExtension.AcquireNextTexture"/> and
/// <see cref="NriSwapChainExtension.QueuePresent"/> report besides throwing. An enum rather than a
/// <c>bool</c> so that a further non-failure outcome can be added without changing the signatures;
/// switch with a default branch.
/// </summary>
public enum NriSwapChainStatus
{
    /// <summary><c>SUCCESS</c>.</summary>
    Ok,

    /// <summary><c>OUT_OF_DATE</c> (Vulkan): the swap chain no longer matches its window; recreate it.</summary>
    OutOfDate,

    /// <summary>
    /// <c>NOT_READY</c> (Vulkan, <see cref="NriSwapChainExtension.TryCreateSwapChain"/>): the window has
    /// no drawable area, typically while minimised; no swap chain was created. Retry later.
    /// </summary>
    NotReady,
}
