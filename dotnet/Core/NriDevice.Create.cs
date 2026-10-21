// L2 — the device's Create* surface, one method per CoreInterface entry, plus the two Helper
// entries that belong beside them.

using PrismNRI.Native;
using Buffer = PrismNRI.Native.Buffer;

namespace PrismNRI;

public sealed unsafe partial class NriDevice
{
    public NriFence CreateFence(ulong initialValue = 0)
    {
        Fence* handle = null;
        Check(Core.CreateFence(Raw, initialValue, &handle), "CreateFence");
        return new NriFence(this, handle);
    }

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
    /// which resource went into which allocation, so these, unlike the memory of
    /// <see cref="BindBufferMemory(ReadOnlySpan{NriBufferMemoryBinding})"/> and the placed
    /// creations, do not refuse disposal while their resources are alive: NRI validation then
    /// reports the resources and leaves the allocation unfreed, and on D3D12 later device creation
    /// in the process fails.
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

        var result = new NriMemory[count];
        for (var i = 0; i < count; i++) result[i] = new NriMemory(this, handles[i]);
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
