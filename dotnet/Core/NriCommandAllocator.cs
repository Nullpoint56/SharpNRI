// L2 — a command allocator.
//
// Created from a queue, which fixes the queue type of every command buffer it backs. Reset
// recycles the memory of all of them at once; none may be pending on the GPU.

using PrismNRI.Native;

namespace PrismNRI;

/// <summary>Backing memory for command buffers of one queue type.</summary>
public sealed unsafe class NriCommandAllocator : NriObject
{
    internal NriCommandAllocator(NriQueue queue, CommandAllocator* handle) : base(handle, "NriCommandAllocator")
    {
        Queue = queue;
        queue.Device.ChildCreated();
    }

    public NriQueue Queue { get; }
    public override NriDevice Device => Queue.Device;

    internal CommandAllocator* Raw => (CommandAllocator*)Handle;

    /// <summary>
    /// A command buffer backed by this allocator. Only one command buffer of an allocator may be
    /// recording at a time: on D3D12 a second <see cref="NriCommandBuffer.Begin"/> while another
    /// records draws debug error 540 and throws <see cref="NriException"/>; Vulkan allows it, so
    /// nothing else tells you. One allocator per command buffer in flight is the simplest shape.
    /// </summary>
    public NriCommandBuffer CreateCommandBuffer()
    {
        CommandBuffer* handle = null;
        Device.Check(Device.Core.CreateCommandBuffer(Raw, &handle), "CreateCommandBuffer");
        return new NriCommandBuffer(this, handle);
    }

    /// <summary>Recycles every command buffer's memory. Nothing recorded from it may still be executing.</summary>
    public void Reset() => Device.Core.ResetCommandAllocator(Raw);

    private protected override void Destroy(void* handle) => Device.Core.DestroyCommandAllocator((CommandAllocator*)handle);
}
