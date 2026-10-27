// L2 — the device's Create* surface, one method per CoreInterface entry, plus the two Helper
// entries that belong beside them.

using PrismNRI.Native;
using Buffer = PrismNRI.Native.Buffer;

namespace PrismNRI;

public sealed unsafe partial class NriDevice
{
    /// <summary>A timeline fence (NRI's <c>FenceDesc</c>: <paramref name="initialValue"/>; <see cref="NriSwapChainExtension.SwapChainSemaphore"/> makes it a binary semaphore).</summary>
    public NriFence CreateFence(ulong initialValue = 0) => CreateFence(new FenceDesc { initialValue = initialValue });

    public NriFence CreateFence(in FenceDesc desc)
    {
        Fence* handle = null;
        fixed (FenceDesc* p = &desc) Check(Core.CreateFence(Raw, p, &handle), "CreateFence");
        return new NriFence(this, handle);
    }

    /// <summary>
    /// A timeline fence that can be exported (<c>FenceDesc::shared</c>; needs
    /// <c>Desc.features.sharedFences</c>): Vulkan <c>VkExportSemaphoreCreateInfo</c>, D3D12
    /// <c>D3D12_FENCE_FLAG_SHARED</c>. Export it with <see cref="NriFence.GetSharedHandle"/>. Throws
    /// <see cref="NriUnsupportedException"/> where Vulkan cannot export a semaphore of that type.
    /// <see cref="NriSwapChainExtension.SwapChainSemaphore"/> as <paramref name="initialValue"/> makes it
    /// binary instead (<see cref="CreateSharedBinaryFence"/>).
    /// </summary>
    public NriFence CreateSharedFence(ulong initialValue = 0) => CreateFence(new FenceDesc { initialValue = initialValue, shared = 1 });

    /// <summary>
    /// An exportable binary semaphore (<c>FenceDesc { SWAPCHAIN_SEMAPHORE, shared }</c>), the object an
    /// external compositor imports as an opaque NT handle or file descriptor. Vulkan only; D3D12 has
    /// no binary fences and throws <see cref="NriUnsupportedException"/>.
    /// </summary>
    /// <remarks>
    /// Binary semantics: every signal must be followed by exactly one wait before the next signal,
    /// across both sides of the export. In <see cref="NriFenceSubmit"/> the value is ignored (pass 0);
    /// <see cref="NriFence.Value"/> and <see cref="NriFence.Wait"/> must not be used on it.
    /// </remarks>
    public NriFence CreateSharedBinaryFence() => CreateSharedFence(NriApi.SWAPCHAIN_SEMAPHORE);

    public NriDescriptorPool CreateDescriptorPool(in DescriptorPoolDesc desc)
    {
        DescriptorPool* handle = null;
        fixed (DescriptorPoolDesc* p = &desc) Check(Core.CreateDescriptorPool(Raw, p, &handle), "CreateDescriptorPool");
        return new NriDescriptorPool(this, handle);
    }

    public NriPipelineLayout CreatePipelineLayout(NriPipelineLayoutDesc desc) => NriPipelineLayout.Create(this, desc);

    public NriPipeline CreateGraphicsPipeline(NriGraphicsPipelineDesc desc) => NriPipeline.CreateGraphics(this, desc);

    public NriPipeline CreateComputePipeline(NriComputePipelineDesc desc) => NriPipeline.CreateCompute(this, desc);

    /// <summary>A pipeline cache, primed with <paramref name="data"/> from an earlier <see cref="NriPipelineCache.GetData"/>, or empty.</summary>
    /// <exception cref="NriException">With <see cref="Result.OUT_OF_DATE"/> when the data is stale; start over with an empty cache.</exception>
    public NriPipelineCache CreatePipelineCache(ReadOnlySpan<byte> data = default)
    {
        PipelineCache* handle = null;
        fixed (byte* p = data)
        {
            var desc = new PipelineCacheDesc { data = p, size = (ulong)data.Length };
            Check(Core.CreatePipelineCache(Raw, &desc, &handle), "CreatePipelineCache");
        }
        return new NriPipelineCache(this, handle);
    }

    public NriQueryPool CreateQueryPool(QueryType type, uint capacity)
    {
        QueryPool* handle = null;
        var desc = new QueryPoolDesc { queryType = type, capacity = capacity };
        Check(Core.CreateQueryPool(Raw, &desc, &handle), "CreateQueryPool");
        return new NriQueryPool(this, handle, type, capacity);
    }

    public NriDescriptor CreateSampler(in SamplerDesc desc)
    {
        Descriptor* handle = null;
        fixed (SamplerDesc* p = &desc) Check(Core.CreateSampler(Raw, p, &handle), "CreateSampler");
        return new NriDescriptor(this, handle, NriDescriptorKind.Sampler);
    }

    // ---------------------------------------------------------------- memory, Vulkan style

    public NriMemory AllocateMemory(in AllocateMemoryDesc desc)
    {
        Memory* handle = null;
        fixed (AllocateMemoryDesc* p = &desc) Check(Core.AllocateMemory(Raw, p, &handle), "AllocateMemory");
        return new NriMemory(this, handle);
    }

    /// <summary>An unbound buffer. Bind it with <see cref="BindBufferMemory"/> before use.</summary>
    public NriBuffer CreateBuffer(in BufferDesc desc)
    {
        Buffer* handle = null;
        fixed (BufferDesc* p = &desc) Check(Core.CreateBuffer(Raw, p, &handle), "CreateBuffer");
        return new NriBuffer(this, handle);
    }

    /// <summary>An unbound texture. Bind it with <see cref="BindTextureMemory"/> before use.</summary>
    public NriTexture CreateTexture(in TextureDesc desc)
    {
        Texture* handle = null;
        fixed (TextureDesc* p = &desc) Check(Core.CreateTexture(Raw, p, &handle), "CreateTexture");
        return new NriTexture(this, handle);
    }

    /// <summary>Binds each buffer into its memory; each memory refuses disposal until its buffers are disposed.</summary>
    public void BindBufferMemory(ReadOnlySpan<NriBufferMemoryBinding> bindings)
    {
        var native = stackalloc BindBufferMemoryDesc[bindings.Length];
        for (var i = 0; i < bindings.Length; i++) native[i] = bindings[i].ToNative();
        Check(Core.BindBufferMemory(native, (uint)bindings.Length), "BindBufferMemory");
        foreach (ref readonly var binding in bindings) binding.Buffer.BoundTo(binding.Memory);
    }

    /// <summary>Binds <paramref name="buffer"/> into <paramref name="memory"/>, which refuses disposal until the buffer is disposed.</summary>
    public void BindBufferMemory(NriBuffer buffer, NriMemory memory, ulong offset = 0) =>
        BindBufferMemory([new NriBufferMemoryBinding(buffer, memory, offset)]);

    /// <summary>Binds each texture into its memory; each memory refuses disposal until its textures are disposed.</summary>
    public void BindTextureMemory(ReadOnlySpan<NriTextureMemoryBinding> bindings)
    {
        var native = stackalloc BindTextureMemoryDesc[bindings.Length];
        for (var i = 0; i < bindings.Length; i++) native[i] = bindings[i].ToNative();
        Check(Core.BindTextureMemory(native, (uint)bindings.Length), "BindTextureMemory");
        foreach (ref readonly var binding in bindings) binding.Texture.BoundTo(binding.Memory);
    }

    /// <summary>Binds <paramref name="texture"/> into <paramref name="memory"/>, which refuses disposal until the texture is disposed.</summary>
    public void BindTextureMemory(NriTexture texture, NriMemory memory, ulong offset = 0) =>
        BindTextureMemory([new NriTextureMemoryBinding(texture, memory, offset)]);

    // ---------------------------------------------------------------- memory, D3D12 style

    /// <summary>Memory requirements for a buffer that does not exist yet. Requires <c>features.getMemoryDesc2</c>.</summary>
    public MemoryDesc GetBufferMemoryDesc(in BufferDesc desc, MemoryLocation location)
    {
        MemoryDesc memory;
        fixed (BufferDesc* p = &desc) Core.GetBufferMemoryDesc2(Raw, p, location, &memory);
        return memory;
    }

    /// <summary>Memory requirements for a texture that does not exist yet. Requires <c>features.getMemoryDesc2</c>.</summary>
    public MemoryDesc GetTextureMemoryDesc(in TextureDesc desc, MemoryLocation location)
    {
        MemoryDesc memory;
        fixed (TextureDesc* p = &desc) Core.GetTextureMemoryDesc2(Raw, p, location, &memory);
        return memory;
    }

    /// <summary>A buffer with its own memory, allocated through VMA or D3D12MA.</summary>
    public NriBuffer CreateCommittedBuffer(MemoryLocation location, in BufferDesc desc, float priority = 0)
    {
        Buffer* handle = null;
        fixed (BufferDesc* p = &desc) Check(Core.CreateCommittedBuffer(Raw, location, priority, p, &handle), "CreateCommittedBuffer");
        return new NriBuffer(this, handle);
    }

    /// <summary>A texture with its own memory, allocated through VMA or D3D12MA.</summary>
    public NriTexture CreateCommittedTexture(MemoryLocation location, in TextureDesc desc, float priority = 0)
    {
        Texture* handle = null;
        fixed (TextureDesc* p = &desc) Check(Core.CreateCommittedTexture(Raw, location, priority, p, &handle), "CreateCommittedTexture");
        return new NriTexture(this, handle);
    }

    /// <summary>
    /// A committed buffer in <see cref="MemoryLocation.DEVICE"/> with a dedicated, exportable allocation
    /// (<c>BufferDesc::shared</c>; needs <c>Desc.features.sharedResources</c>): Vulkan
    /// opaque handle, D3D12 <c>D3D12_HEAP_FLAG_SHARED</c>. Export it with <see cref="NriBuffer.GetSharedHandle"/>;
    /// everything else about it is an ordinary <see cref="NriBuffer"/>. Throws
    /// <see cref="NriUnsupportedException"/> where Vulkan cannot export a buffer of that usage.
    /// </summary>
    public NriBuffer CreateSharedBuffer(in BufferDesc desc, float priority = 0)
    {
        var shared = desc;
        shared.shared = 1;
        return CreateCommittedBuffer(MemoryLocation.DEVICE, in shared, priority);
    }

    /// <summary>
    /// A committed texture in <see cref="MemoryLocation.DEVICE"/> with a dedicated, exportable
    /// allocation (<c>TextureDesc::shared</c>; needs <c>Desc.features.sharedResources</c>):
    /// Vulkan opaque handle, D3D12 <c>D3D12_HEAP_FLAG_SHARED</c>. Export it with
    /// <see cref="NriTexture.GetSharedHandle"/>; everything else about it is an ordinary <see cref="NriTexture"/>.
    /// Throws <see cref="NriUnsupportedException"/> where Vulkan cannot export an image of that format,
    /// usage, size or sample count.
    /// </summary>
    public NriTexture CreateSharedTexture(in TextureDesc desc, float priority = 0)
    {
        var shared = desc;
        shared.shared = 1;
        return CreateCommittedTexture(MemoryLocation.DEVICE, in shared, priority);
    }

    /// <summary>A buffer placed into existing memory at an offset; the memory refuses disposal until the buffer is disposed.</summary>
    public NriBuffer CreatePlacedBuffer(NriMemory memory, ulong offset, in BufferDesc desc)
    {
        Buffer* handle = null;
        fixed (BufferDesc* p = &desc) Check(Core.CreatePlacedBuffer(Raw, memory.Raw, offset, p, &handle), "CreatePlacedBuffer");
        var buffer = new NriBuffer(this, handle);
        buffer.BoundTo(memory);
        return buffer;
    }

    /// <summary>A texture placed into existing memory at an offset; the memory refuses disposal until the texture is disposed.</summary>
    public NriTexture CreatePlacedTexture(NriMemory memory, ulong offset, in TextureDesc desc)
    {
        Texture* handle = null;
        fixed (TextureDesc* p = &desc) Check(Core.CreatePlacedTexture(Raw, memory.Raw, offset, p, &handle), "CreatePlacedTexture");
        var texture = new NriTexture(this, handle);
        texture.BoundTo(memory);
        return texture;
    }

    // ---------------------------------------------------------------- helper

    /// <summary>
    /// Allocates and binds memory for a group of unbound resources in as few allocations as NRI
    /// can manage. Returns the allocations; dispose them after the resources. NRI does not say
    /// which resource went into which allocation, so each allocation refuses disposal, throwing
    /// <see cref="InvalidOperationException"/>, while any resource of this call is alive (see
    /// <see cref="NriMemory"/>).
    /// </summary>
    public NriMemory[] AllocateAndBindMemory(MemoryLocation location, ReadOnlySpan<NriTexture> textures, ReadOnlySpan<NriBuffer> buffers, ulong preferredMemorySize = 0, float residencyPriority = 0, bool vma = false)
    {
        var rawTextures = stackalloc Texture*[textures.Length];
        for (var i = 0; i < textures.Length; i++) rawTextures[i] = textures[i].Raw;
        var rawBuffers = stackalloc Buffer*[buffers.Length];
        for (var i = 0; i < buffers.Length; i++) rawBuffers[i] = buffers[i].Raw;

        var desc = new ResourceGroupDesc
        {
            memoryLocation = location,
            textures = rawTextures,
            textureNum = (uint)textures.Length,
            buffers = rawBuffers,
            bufferNum = (uint)buffers.Length,
            preferredMemorySize = preferredMemorySize,
            residencyPriority = residencyPriority,
            vma = Bool(vma),
        };

        var count = Helper.CalculateAllocationNumber(Raw, &desc);
        var handles = stackalloc Memory*[checked((int)count)];
        Check(Helper.AllocateAndBindMemory(Raw, &desc, handles), "AllocateAndBindMemory");

        // The allocations share one count, so each resource of the call holds every one of them.
        var result = new NriMemory[count];
        var group = new NriMemory.Holders(group: true);
        for (var i = 0; i < count; i++) result[i] = new NriMemory(this, handles[i], group);
        if (count == 0) return result; // NONE allocates nothing
        foreach (var texture in textures) texture.BoundTo(result[0]);
        foreach (var buffer in buffers) buffer.BoundTo(result[0]);
        return result;
    }

    /// <summary>The OS-reported video memory budget and current usage for a location.</summary>
    public VideoMemoryInfo QueryVideoMemoryInfo(MemoryLocation location)
    {
        VideoMemoryInfo info;
        Check(Helper.QueryVideoMemoryInfo(Raw, location, &info), "QueryVideoMemoryInfo");
        return info;
    }
}
