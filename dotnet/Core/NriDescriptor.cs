// L2 — a descriptor: a sampler, a buffer view or a texture view.
//
// NRI has one Descriptor type for all three and one DestroyDescriptor. The kind is kept on the
// wrapper only so a mistake reads as a message rather than a validation-layer error.

using SharpNRI.Native;

namespace SharpNRI;

public enum NriDescriptorKind : byte
{
    Sampler,
    BufferView,
    TextureView,
}

/// <summary>A descriptor. Written into descriptor sets, bound as an attachment, or set as a root descriptor.</summary>
public sealed unsafe class NriDescriptor : NriObject
{
    internal NriDescriptor(NriDevice device, Descriptor* handle, NriDescriptorKind kind) : base(handle, "NriDescriptor")
    {
        Device = device;
        Kind = kind;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    public NriDescriptorKind Kind { get; }

    internal Descriptor* Raw => (Descriptor*)Handle;

    /// <summary>
    /// <c>D3D12_CPU_DESCRIPTOR_HANDLE</c> on D3D12; <c>VkImageView</c>, <c>VkBufferView</c> or
    /// <c>VkSampler</c> on Vulkan. Last resort.
    /// </summary>
    public ulong GetNativeObject() => Device.Core.GetDescriptorNativeObject(Raw);

    private protected override void Destroy(void* handle) => Device.Core.DestroyDescriptor((Descriptor*)handle);
}
