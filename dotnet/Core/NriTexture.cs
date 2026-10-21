// L2 — a texture.

using PrismNRI.Native;

namespace PrismNRI;

/// <summary>A texture. Unbound until placed in memory unless created committed or placed.</summary>
public sealed unsafe class NriTexture : NriObject
{
    private readonly bool _owned;
    private NriMemory.Holders? _memory; // of the memory bound into or placed in through the binding, which refuses disposal while set

    internal NriTexture(NriDevice device, Texture* handle) : base(handle, "NriTexture")
    {
        Device = device;
        _owned = true;
        device.ChildCreated();
    }

    /// <summary>
    /// A texture another NRI object owns (a swap chain's): not counted as a device child, and
    /// disposing it is a no-op. The owner destroys it.
    /// </summary>
    internal NriTexture(NriDevice device, Texture* handle, bool owned) : base(handle, "NriTexture")
    {
        Device = device;
        _owned = owned;
        if (owned) device.ChildCreated();
    }

    public override NriDevice Device { get; }

    /// <summary>False for a texture NRI owns (a swap chain texture): disposing it is a no-op.</summary>
    public bool IsOwned => _owned;

    internal Texture* Raw => (Texture*)Handle;

    /// <summary>The desc NRI holds for this texture, with NRI's defaults filled in.</summary>
    public ref readonly TextureDesc Desc => ref *Device.Core.GetTextureDesc(Raw);

    /// <summary>Memory requirements for binding this texture into memory at <paramref name="location"/>.</summary>
    public MemoryDesc GetMemoryDesc(MemoryLocation location)
    {
        MemoryDesc desc;
        Device.Core.GetTextureMemoryDesc(Raw, location, &desc);
        return desc;
    }

    /// <summary>
    /// A view for binding as a descriptor or attachment. <paramref name="desc"/>'s <c>texture</c>
    /// field is set by this call; leave it null.
    /// </summary>
    public NriDescriptor CreateView(TextureViewDesc desc)
    {
        desc.texture = Raw;
        Descriptor* view = null;
        Device.Check(Device.Core.CreateTextureView(&desc, &view), "CreateTextureView");
        return new NriDescriptor(Device, view, NriDescriptorKind.TextureView);
    }

    /// <summary>A view of the whole texture with the given usage and format.</summary>
    public NriDescriptor CreateView(TextureView type, Format format) =>
        CreateView(new TextureViewDesc
        {
            type = type,
            format = format,
            mipNum = NriApi.REMAINING,
            layerNum = NriApi.REMAINING,
            sliceNum = NriApi.REMAINING,
            planes = PlaneBits.ALL,
        });

    /// <summary><c>ID3D12Resource*</c> on D3D12, <c>VkImage</c> on Vulkan. Last resort.</summary>
    public ulong GetNativeObject() => Device.Core.GetTextureNativeObject(Raw);

    /// <summary>Bound into or placed in <paramref name="memory"/>, which then outlives this texture.</summary>
    internal void BoundTo(NriMemory memory) => NriMemory.Track(ref _memory, memory);

    private protected override void Destroy(void* handle)
    {
        if (_owned) Device.Core.DestroyTexture((Texture*)handle);
    }

    private protected override void OnDestroyed()
    {
        NriMemory.Release(ref _memory);
        if (_owned) base.OnDestroyed();
    }
}
