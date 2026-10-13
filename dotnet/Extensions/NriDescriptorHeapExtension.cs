// L3 — the DescriptorHeap extension (NRIDescriptorHeap.h).
//
// Layout-free, directly indexed descriptor heaps: SM 6.6 ResourceDescriptorHeap /
// SamplerDescriptorHeap on D3D12, VK_EXT_descriptor_heap on Vulkan. Available when
// DeviceDesc.features.descriptorHeap is set; UNSUPPORTED otherwise. Writes happen on the host
// only: NRI offers no GPU-side update because D3D12 has none.
//
// Threadsafe per NRI, except that writes to overlapping descriptor indices need external
// synchronization. CmdSetDescriptorHeap follows the command buffer's rule: one recording thread.

using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary><c>WriteResourceDescriptorsDesc</c> naming the wrapper.</summary>
/// <param name="Resource">Any descriptor except <c>MUTABLE</c>, <c>INPUT_ATTACHMENT</c> and <c>SAMPLER</c>.</param>
/// <param name="DescriptorIndex">Slot in the resource heap.</param>
public readonly unsafe record struct NriWriteResourceDescriptorsDesc(NriDescriptor Resource, uint DescriptorIndex)
{
    internal WriteResourceDescriptorsDesc ToNative() => new() { resource = Resource.Raw, descriptorIndex = DescriptorIndex };
}

/// <summary><c>WriteSamplerDescriptorsDesc</c> naming the wrapper.</summary>
/// <param name="Sampler">Must be a sampler.</param>
/// <param name="DescriptorIndex">Slot in the sampler heap.</param>
public readonly unsafe record struct NriWriteSamplerDescriptorsDesc(NriDescriptor Sampler, uint DescriptorIndex)
{
    internal WriteSamplerDescriptorsDesc ToNative() => new() { sampler = Sampler.Raw, descriptorIndex = DescriptorIndex };
}

/// <summary>A directly indexed descriptor heap. Created by <see cref="NriDescriptorHeapExtension.CreateDescriptorHeap(in DescriptorHeapDesc)"/>.</summary>
public sealed unsafe class NriDescriptorHeap : NriObject
{
    private readonly NriDescriptorHeapExtension _extension;

    internal NriDescriptorHeap(NriDescriptorHeapExtension extension, DescriptorHeap* handle, in DescriptorHeapDesc desc) : base(handle, "NriDescriptorHeap")
    {
        _extension = extension;
        Desc = desc;
        extension.Device.ChildCreated();
    }

    public override NriDevice Device => _extension.Device;

    /// <summary>The capacities the heap was created with.</summary>
    public DescriptorHeapDesc Desc { get; }

    internal DescriptorHeap* Raw => (DescriptorHeap*)Handle;

    private protected override void Destroy(void* handle) => _extension.DestroyDescriptorHeap((DescriptorHeap*)handle);
}

/// <summary>NRI's <c>DescriptorHeapInterface</c>. Acquire through <see cref="NriDevice.DescriptorHeap"/>.</summary>
public sealed unsafe class NriDescriptorHeapExtension
{
    private readonly DescriptorHeapInterface _table;

    internal NriDescriptorHeapExtension(NriDevice device, in DescriptorHeapInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriDescriptorHeapExtension? extension)
    {
        if (InterfaceTable.TryAcquire<DescriptorHeapInterface>(device.Raw, "DescriptorHeapInterface", out var table))
        {
            extension = new NriDescriptorHeapExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// A heap with room for <c>resourceDescriptorNum</c> resource and <c>samplerDescriptorNum</c>
    /// sampler descriptors. At least one capacity must be non-zero; 0 means no heap of that kind.
    /// </summary>
    public NriDescriptorHeap CreateDescriptorHeap(in DescriptorHeapDesc descriptorHeapDesc)
    {
        DescriptorHeap* handle = null;
        fixed (DescriptorHeapDesc* p = &descriptorHeapDesc) Device.Check(_table.CreateDescriptorHeap(Device.Raw, p, &handle), "CreateDescriptorHeap");
        return new NriDescriptorHeap(this, handle, descriptorHeapDesc);
    }

    /// <summary><see cref="CreateDescriptorHeap(in DescriptorHeapDesc)"/> with the capacities inline.</summary>
    public NriDescriptorHeap CreateDescriptorHeap(uint resourceDescriptorNum, uint samplerDescriptorNum = 0) =>
        CreateDescriptorHeap(new DescriptorHeapDesc { resourceDescriptorNum = resourceDescriptorNum, samplerDescriptorNum = samplerDescriptorNum });

    internal void DestroyDescriptorHeap(DescriptorHeap* heap) => _table.DestroyDescriptorHeap(heap);

    /// <summary>
    /// Host-side writes of resource views into the heap. Writes to overlapping indices from
    /// several threads need external synchronization. No heap allocation.
    /// </summary>
    public void WriteResourceDescriptors(NriDescriptorHeap descriptorHeap, ReadOnlySpan<NriWriteResourceDescriptorsDesc> writeDescs)
    {
        var native = stackalloc WriteResourceDescriptorsDesc[writeDescs.Length];
        for (var i = 0; i < writeDescs.Length; i++) native[i] = writeDescs[i].ToNative();
        Device.Check(_table.WriteResourceDescriptors(descriptorHeap.Raw, native, (uint)writeDescs.Length), "WriteResourceDescriptors");
    }

    /// <summary>Host-side writes of samplers into the heap. No heap allocation.</summary>
    public void WriteSamplerDescriptors(NriDescriptorHeap descriptorHeap, ReadOnlySpan<NriWriteSamplerDescriptorsDesc> writeDescs)
    {
        var native = stackalloc WriteSamplerDescriptorsDesc[writeDescs.Length];
        for (var i = 0; i < writeDescs.Length; i++) native[i] = writeDescs[i].ToNative();
        Device.Check(_table.WriteSamplerDescriptors(descriptorHeap.Raw, native, (uint)writeDescs.Length), "WriteSamplerDescriptors");
    }

    /// <summary><c>CmdSetDescriptorHeap</c>: binds the heap for shaders that index it directly.</summary>
    public void SetDescriptorHeap(NriCommandBuffer commandBuffer, NriDescriptorHeap descriptorHeap) =>
        _table.CmdSetDescriptorHeap(commandBuffer.Raw, descriptorHeap.Raw);
}
