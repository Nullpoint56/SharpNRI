// L2 — a memory allocation.
//
// NRI's memory model is Vulkan-shaped: create a resource, ask what memory it needs, allocate,
// bind. Memory is destroyed with FreeMemory, after everything bound to it.

using PrismNRI.Native;
using Buffer = PrismNRI.Native.Buffer;

namespace PrismNRI;

/// <summary>A device memory allocation that buffers and textures are bound into.</summary>
public sealed unsafe class NriMemory : NriObject
{
    internal NriMemory(NriDevice device, Memory* handle) : base(handle, "NriMemory")
    {
        Device = device;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    internal Memory* Raw => (Memory*)Handle;

    private protected override void Destroy(void* handle) => Device.Core.FreeMemory((Memory*)handle);
}

/// <summary>One buffer to bind into one memory at an offset. Bindings may overlap (aliasing).</summary>
public readonly unsafe record struct NriBufferMemoryBinding(NriBuffer Buffer, NriMemory Memory, ulong Offset = 0)
{
    internal BindBufferMemoryDesc ToNative() => new() { buffer = Buffer.Raw, memory = Memory.Raw, offset = Offset };
}

/// <summary>One texture to bind into one memory at an offset. Bindings may overlap (aliasing).</summary>
public readonly unsafe record struct NriTextureMemoryBinding(NriTexture Texture, NriMemory Memory, ulong Offset = 0)
{
    internal BindTextureMemoryDesc ToNative() => new() { texture = Texture.Raw, memory = Memory.Raw, offset = Offset };
}
