// L2 — a buffer.

using PrismNRI.Native;
using Buffer = PrismNRI.Native.Buffer;

namespace PrismNRI;

/// <summary>A buffer. Unbound until placed in memory unless created committed or placed.</summary>
public sealed unsafe class NriBuffer : NriObject
{
    private readonly bool _owned;
    private NriMemory? _memory; // bound into or placed in through the binding; NriMemory refuses disposal while set

    internal NriBuffer(NriDevice device, Buffer* handle, bool owned = true) : base(handle, "NriBuffer")
    {
        _device = device;
        _owned = owned;
        if (owned) device.ChildCreated();
    }

    /// <summary>
    /// The <c>HAS_BUFFER</c> sentinel: tells an <see cref="NriAccelerationStructureDesc"/> that a
    /// buffer will be present at build time without naming one. Valid nowhere else.
    /// </summary>
    public static NriBuffer HasBuffer { get; } = new(null!, NriApi.HAS_BUFFER, owned: false);

    private readonly NriDevice _device = null!;

    /// <exception cref="InvalidOperationException">On <see cref="HasBuffer"/>, which belongs to no device.</exception>
    public override NriDevice Device => _device ?? throw new InvalidOperationException("NriBuffer.HasBuffer is a sentinel for AccelerationStructureDesc and has no device, desc or view.");

    /// <summary>False for a buffer NRI owns (an acceleration structure's backing buffer): disposing it is a no-op.</summary>
    public bool IsOwned => _owned;

    internal Buffer* Raw => (Buffer*)Handle;

    /// <summary>The desc NRI holds for this buffer.</summary>
    public ref readonly BufferDesc Desc => ref *Device.Core.GetBufferDesc(Raw);

    /// <summary>Memory requirements for binding this buffer into memory at <paramref name="location"/>.</summary>
    public MemoryDesc GetMemoryDesc(MemoryLocation location)
    {
        MemoryDesc desc;
        Device.Core.GetBufferMemoryDesc(Raw, location, &desc);
        return desc;
    }

    /// <summary>
    /// Maps a host-visible range. Persistent on D3D12 and Vulkan; <see cref="Unmap"/> flushes
    /// where the memory is not host-coherent. Not valid on <c>DEVICE</c> memory.
    /// </summary>
    /// <exception cref="NriException">
    /// With <see cref="Result.FAILURE"/> when NRI maps nothing (it returns null), carrying the error
    /// text NRI reported for this call, if it reported one (NONE maps nothing and says nothing).
    /// </exception>
    public Span<byte> Map(ulong offset, ulong size)
    {
        var p = Device.Core.MapBuffer(Raw, offset, size);
        Device.Check(p is null ? Result.FAILURE : Result.SUCCESS, "MapBuffer");
        var length = size == NriApi.WHOLE_SIZE ? Desc.size - offset : size;
        return new Span<byte>(p, checked((int)length));
    }

    /// <summary>Maps the whole buffer.</summary>
    public Span<byte> Map() => Map(0, NriApi.WHOLE_SIZE);

    public void Unmap() => Device.Core.UnmapBuffer(Raw);

    /// <summary>GPU virtual address, or 0 when the backend does not support it.</summary>
    public ulong DeviceAddress => Device.Core.GetBufferDeviceAddress(Raw);

    /// <summary>
    /// A view for binding as a descriptor. <paramref name="desc"/>'s <c>buffer</c> field is set by
    /// this call; leave it null.
    /// </summary>
    public NriDescriptor CreateView(BufferViewDesc desc)
    {
        desc.buffer = Raw;
        Descriptor* view = null;
        Device.Check(Device.Core.CreateBufferView(&desc, &view), "CreateBufferView");
        return new NriDescriptor(Device, view, NriDescriptorKind.BufferView);
    }

    public NriDescriptor CreateView(BufferView type, ulong offset = 0, ulong size = NriApi.WHOLE_SIZE, Format format = Format.UNKNOWN, uint structureStride = 0) =>
        CreateView(new BufferViewDesc { type = type, offset = offset, size = size, format = format, structureStride = structureStride });

    /// <summary><c>ID3D12Resource*</c> on D3D12, <c>VkBuffer</c> on Vulkan. Last resort.</summary>
    public ulong GetNativeObject() => Device.Core.GetBufferNativeObject(Raw);

    /// <summary>Bound into or placed in <paramref name="memory"/>, which then outlives this buffer.</summary>
    internal void BoundTo(NriMemory memory) => NriMemory.Track(ref _memory, memory);

    private protected override void Destroy(void* handle)
    {
        if (_owned) Device.Core.DestroyBuffer((Buffer*)handle);
    }

    private protected override void OnDestroyed()
    {
        NriMemory.Release(ref _memory);
        if (_owned) base.OnDestroyed();
    }
}
