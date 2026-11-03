// L3 — the WrapperD3D12 extension (NRIWrapperD3D12.h). Last resort.
//
// Wraps D3D12 objects the caller created into NRI objects on a D3D12 device. COM pointers are nint
// (ID3D12Resource*, ID3D12Heap*, ...); DXGI_FORMAT is int. Available on D3D12 devices only;
// UNSUPPORTED elsewhere. Threadsafe per NRI.
//
// CreateAccelerationStructureD3D12 returns the RayTracing extension's NriAccelerationStructure,
// which is destroyed through that extension's table, so it needs the extension; the caller names
// the structure type, which AccelerationStructureD3D12Desc does not carry. Device creation from an
// ID3D12Device is NriDevice.CreateFromD3D12.

using SharpNRI.Interop;
using SharpNRI.Native;
using Buffer = SharpNRI.Native.Buffer;

namespace SharpNRI;

/// <summary>NRI's <c>WrapperD3D12Interface</c>. Acquire through <see cref="NriDevice.WrapperD3D12"/>.</summary>
public sealed unsafe class NriWrapperD3D12Extension
{
    private readonly WrapperD3D12Interface _table;

    internal NriWrapperD3D12Extension(NriDevice device, in WrapperD3D12Interface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriWrapperD3D12Extension? extension)
    {
        if (InterfaceTable.TryAcquire<WrapperD3D12Interface>(device.Raw, "WrapperD3D12Interface", out var table))
        {
            extension = new NriWrapperD3D12Extension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// Wraps an <c>ID3D12CommandList*</c>. <paramref name="d3d12CommandAllocator"/>
    /// (<c>ID3D12CommandAllocator*</c>) is needed only for <c>BeginCommandBuffer</c>. No NRI
    /// allocator backs the result. NRI's validation counts it as recording from the start and
    /// refuses <see cref="NriCommandBuffer.Begin"/>: pass it open, set the descriptor pool with
    /// <see cref="NriCommandBuffer.SetDescriptorPool"/>, record, <see cref="NriCommandBuffer.End"/>.
    /// </summary>
    public NriCommandBuffer CreateCommandBufferD3D12(nint d3d12CommandList, nint d3d12CommandAllocator = 0)
    {
        var desc = new CommandBufferD3D12Desc
        {
            d3d12CommandList = (ID3D12CommandList*)d3d12CommandList,
            d3d12CommandAllocator = (ID3D12CommandAllocator*)d3d12CommandAllocator,
        };
        CommandBuffer* handle = null;
        Device.Check(_table.CreateCommandBufferD3D12(Device.Raw, &desc, &handle), "CreateCommandBufferD3D12");
        return new NriCommandBuffer(Device, handle);
    }

    /// <summary>
    /// Wraps a pair of shader-visible <c>ID3D12DescriptorHeap*</c> (resources, samplers).
    /// <paramref name="descriptorSetMaxNum"/> is NRI's allocation limit, unrelated to D3D12 but
    /// required to match expected usage.
    /// </summary>
    public NriDescriptorPool CreateDescriptorPoolD3D12(nint d3d12ResourceDescriptorHeap, nint d3d12SamplerDescriptorHeap, uint descriptorSetMaxNum)
    {
        var desc = new DescriptorPoolD3D12Desc
        {
            d3d12ResourceDescriptorHeap = (ID3D12DescriptorHeap*)d3d12ResourceDescriptorHeap,
            d3d12SamplerDescriptorHeap = (ID3D12DescriptorHeap*)d3d12SamplerDescriptorHeap,
            descriptorSetMaxNum = descriptorSetMaxNum,
        };
        DescriptorPool* handle = null;
        Device.Check(_table.CreateDescriptorPoolD3D12(Device.Raw, &desc, &handle), "CreateDescriptorPoolD3D12");
        return new NriDescriptorPool(Device, handle);
    }

    /// <summary>
    /// Wraps an <c>ID3D12Resource*</c> buffer. <paramref name="desc"/> is optional, but not all of
    /// it can be read back from the resource; <paramref name="structureStride"/> must be given if
    /// it is used as a structured or raw buffer.
    /// </summary>
    public NriBuffer CreateBufferD3D12(nint d3d12Resource, BufferDesc? desc = null, uint structureStride = 0)
    {
        var bufferDesc = desc ?? default;
        var native = new BufferD3D12Desc
        {
            d3d12Resource = (ID3D12Resource*)d3d12Resource,
            desc = desc is null ? null : &bufferDesc,
            structureStride = structureStride,
        };
        Buffer* handle = null;
        Device.Check(_table.CreateBufferD3D12(Device.Raw, &native, &handle), "CreateBufferD3D12");
        return new NriBuffer(Device, handle);
    }

    /// <summary>
    /// Wraps an <c>ID3D12Resource*</c> texture. <paramref name="format"/> (a <c>DXGI_FORMAT</c>)
    /// must be given, as a compatible typed format, if the resource is typeless.
    /// </summary>
    public NriTexture CreateTextureD3D12(nint d3d12Resource, int format = 0)
    {
        var desc = new TextureD3D12Desc { d3d12Resource = (ID3D12Resource*)d3d12Resource, format = format };
        Texture* handle = null;
        Device.Check(_table.CreateTextureD3D12(Device.Raw, &desc, &handle), "CreateTextureD3D12");
        return new NriTexture(Device, handle);
    }

    /// <summary>Wraps an <c>ID3D12Heap*</c> at an offset as memory.</summary>
    public NriMemory CreateMemoryD3D12(nint d3d12Heap, ulong offset = 0)
    {
        var desc = new MemoryD3D12Desc { d3d12Heap = (ID3D12Heap*)d3d12Heap, offset = offset };
        Memory* handle = null;
        Device.Check(_table.CreateMemoryD3D12(Device.Raw, &desc, &handle), "CreateMemoryD3D12");
        return new NriMemory(Device, handle);
    }

    /// <summary>
    /// Wraps an <c>ID3D12Resource*</c> holding an acceleration structure of <paramref name="type"/>.
    /// <c>size</c>, <c>buildScratchSize</c> and <c>updateScratchSize</c> are what NRI reports back;
    /// it does not query them. Needs the device's RayTracing extension, which destroys it.
    /// </summary>
    /// <exception cref="NriUnsupportedException">When the device has no RayTracing extension.</exception>
    public NriAccelerationStructure CreateAccelerationStructureD3D12(in AccelerationStructureD3D12Desc accelerationStructureD3D12Desc, AccelerationStructureType type)
    {
        var rayTracing = Device.RayTracing;
        AccelerationStructure* handle = null;
        fixed (AccelerationStructureD3D12Desc* p = &accelerationStructureD3D12Desc) Device.Check(_table.CreateAccelerationStructureD3D12(Device.Raw, p, &handle), "CreateAccelerationStructureD3D12");
        return new NriAccelerationStructure(rayTracing, handle, type);
    }

    /// <summary>Wraps an <c>ID3D12Fence*</c>.</summary>
    public NriFence CreateFenceD3D12(nint d3d12Fence)
    {
        var desc = new FenceD3D12Desc { d3d12Fence = (ID3D12Fence*)d3d12Fence };
        Fence* handle = null;
        Device.Check(_table.CreateFenceD3D12(Device.Raw, &desc, &handle), "CreateFenceD3D12");
        return new NriFence(Device, handle);
    }
}
