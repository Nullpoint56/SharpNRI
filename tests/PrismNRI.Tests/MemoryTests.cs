// NriMemory: the Vulkan-style path (create unbound, ask, allocate, bind), the D3D12-style path
// (ask before creating, allocate, place), the Helper's AllocateAndBindMemory over a group, and the
// order resources and memory are disposed in.

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class MemoryTests
{
    private static readonly BufferDesc StorageBuffer = new() { size = 4096, usage = BufferUsageBits.SHADER_RESOURCE | BufferUsageBits.SHADER_RESOURCE_STORAGE };

    private static readonly TextureDesc SampledTexture = new()
    {
        type = TextureType.TEXTURE_2D,
        usage = TextureUsageBits.SHADER_RESOURCE,
        format = Format.RGBA8_UNORM,
        width = 64,
        height = 64,
        depth = 1,
        mipNum = 1,
        layerNum = 1,
        sampleNum = 1,
    };

    private static AllocateMemoryDesc AllocationFor(in MemoryDesc memory) => new() { size = memory.size, type = memory.type };

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Buffer_is_bound_to_memory_allocated_from_its_requirements(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // The resource exists before its memory but must be destroyed first: NRI validation
        // reports "Buffer ... is still bound to the memory" when the memory goes first. "using var"
        // would dispose in reverse declaration order, i.e. the memory first, so this is explicit.
        var buffer = device.CreateBuffer(StorageBuffer);
        NriMemory? memory = null;
        try
        {
            var requirements = buffer.GetMemoryDesc(MemoryLocation.DEVICE);
            Record($"{backend}: 4096-byte storage buffer needs size={requirements.size} alignment={requirements.alignment} type=0x{requirements.type:X} dedicated={requirements.mustBeDedicated}");

            // Measured: VK asks for 4096 bytes aligned to 16, D3D12 for 4096 aligned to 128
            // (Agility SDK device) or 65536/65536 (inbox runtime).
            Assert.True(requirements.size >= StorageBuffer.size, $"size {requirements.size}");
            Assert.NotEqual(0UL, requirements.size);

            memory = device.AllocateMemory(AllocationFor(requirements));
            device.BindBufferMemory(buffer, memory);

            Assert.Equal(2, device.LiveObjects);
            Assert.NotEqual(0UL, buffer.DeviceAddress);
        }
        finally
        {
            buffer.Dispose();
            memory?.Dispose();
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Texture_is_bound_to_memory_allocated_from_its_requirements(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var texture = device.CreateTexture(SampledTexture);
        NriMemory? memory = null;
        try
        {
            var requirements = texture.GetMemoryDesc(MemoryLocation.DEVICE);
            Record($"{backend}: 64x64 RGBA8 texture needs size={requirements.size} alignment={requirements.alignment} type=0x{requirements.type:X} dedicated={requirements.mustBeDedicated}");

            Assert.True(requirements.size >= 64 * 64 * 4, $"size {requirements.size}");
            Assert.NotEqual(0UL, requirements.size);

            memory = device.AllocateMemory(AllocationFor(requirements));
            device.BindTextureMemory(texture, memory);
            using var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);

            Assert.Equal(3, device.LiveObjects);
        }
        finally
        {
            texture.Dispose();
            memory?.Dispose();
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Placed_buffer_and_texture_are_created_from_requirements_asked_before_creation(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.getMemoryDesc2 == 0, $"{backend}: features.getMemoryDesc2 is false");

        var bufferRequirements = device.GetBufferMemoryDesc(StorageBuffer, MemoryLocation.DEVICE);
        var textureRequirements = device.GetTextureMemoryDesc(SampledTexture, MemoryLocation.DEVICE);
        Record($"{backend}: GetBufferMemoryDesc2 size={bufferRequirements.size} alignment={bufferRequirements.alignment} type=0x{bufferRequirements.type:X}; GetTextureMemoryDesc2 size={textureRequirements.size} alignment={textureRequirements.alignment} type=0x{textureRequirements.type:X}");
        Assert.NotEqual(0UL, bufferRequirements.size);
        Assert.NotEqual(0UL, textureRequirements.size);

        using var bufferMemory = device.AllocateMemory(AllocationFor(bufferRequirements));
        using var textureMemory = device.AllocateMemory(AllocationFor(textureRequirements));
        using var buffer = device.CreatePlacedBuffer(bufferMemory, 0, StorageBuffer);
        using var texture = device.CreatePlacedTexture(textureMemory, 0, SampledTexture);
        using var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);

        Assert.Equal(StorageBuffer.size, buffer.Desc.size);
        Assert.Equal(SampledTexture.width, texture.Desc.width);
        Assert.NotEqual(0UL, buffer.DeviceAddress);
        Assert.Equal(5, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Placed_buffers_share_one_allocation_at_aligned_offsets(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.getMemoryDesc2 == 0, $"{backend}: features.getMemoryDesc2 is false");

        var requirements = device.GetBufferMemoryDesc(StorageBuffer, MemoryLocation.DEVICE);
        var stride = Align(requirements.size, Math.Max(requirements.alignment, 1u));
        using var memory = device.AllocateMemory(new AllocateMemoryDesc { size = stride * 2, type = requirements.type });
        using var first = device.CreatePlacedBuffer(memory, 0, StorageBuffer);
        using var second = device.CreatePlacedBuffer(memory, stride, StorageBuffer);

        Assert.Equal(first.DeviceAddress + stride, second.DeviceAddress);
        Assert.Equal(3, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void AllocateAndBindMemory_binds_a_group_of_two_buffers_and_a_texture(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var a = device.CreateBuffer(StorageBuffer);
        using var b = device.CreateBuffer(new BufferDesc { size = 65536, usage = BufferUsageBits.VERTEX });
        using var texture = device.CreateTexture(SampledTexture);

        var memories = device.AllocateAndBindMemory(MemoryLocation.DEVICE, [texture], [a, b]);
        try
        {
            Record($"{backend}: AllocateAndBindMemory(2 buffers + 1 texture, DEVICE) returned {memories.Length} allocation(s)");

            // Measured: VK and D3D12 merge the group into a single allocation.
            Assert.Single(memories);
            Assert.Equal(3 + memories.Length, device.LiveObjects);

            // Bound means usable: a view on the texture and the buffers' addresses.
            using var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
            Assert.NotEqual(0UL, a.DeviceAddress);
        }
        finally
        {
            // Resources first: memory outlives everything bound to it.
            texture.Dispose();
            b.Dispose();
            a.Dispose();
            foreach (var m in memories) m.Dispose();
        }

        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Records NONE's dummy behaviour, not a feature: NONE's CalculateAllocationNumber answers 0, so
    /// AllocateAndBindMemory returns no allocations for any group (ImplNONE.cpp; CLAUDE.md: Device
    /// creation and callbacks). NONE has no validation layer, so there are no messages to check.
    /// </summary>
    [Fact]
    public void NONE_AllocateAndBindMemory_returns_no_allocations()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);

        using var a = device.CreateBuffer(StorageBuffer);
        using var b = device.CreateBuffer(new BufferDesc { size = 65536, usage = BufferUsageBits.VERTEX });
        using var texture = device.CreateTexture(SampledTexture);

        var memories = device.AllocateAndBindMemory(MemoryLocation.DEVICE, [texture], [a, b]);
        Assert.Empty(memories);
        Assert.Equal(3, device.LiveObjects);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Resources_are_disposed_before_their_memory_and_counted(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var buffer = device.CreateBuffer(StorageBuffer);
        var texture = device.CreateTexture(SampledTexture);
        var bufferMemory = device.AllocateMemory(AllocationFor(buffer.GetMemoryDesc(MemoryLocation.DEVICE)));
        var textureMemory = device.AllocateMemory(AllocationFor(texture.GetMemoryDesc(MemoryLocation.DEVICE)));
        device.BindBufferMemory(buffer, bufferMemory);
        device.BindTextureMemory(texture, textureMemory);
        Assert.Equal(4, device.LiveObjects);

        buffer.Dispose();
        Assert.Equal(3, device.LiveObjects);
        texture.Dispose();
        Assert.Equal(2, device.LiveObjects);
        bufferMemory.Dispose();
        Assert.Equal(1, device.LiveObjects);
        textureMemory.Dispose();
        Assert.Equal(0, device.LiveObjects);
        Assert.True(bufferMemory.IsDisposed);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriMemory refuses disposal while a resource bound into it or placed in it through the binding
    /// is alive, before NRI is called. One allocation holds a buffer bound with BindBufferMemory and
    /// a second placed after it with CreatePlacedBuffer; a texture is bound into a second allocation
    /// with BindTextureMemory and another placed into a third with CreatePlacedTexture. Disposing
    /// each allocation throws InvalidOperationException naming its count (2, 1, 1); NRI reports
    /// nothing (its FreeMemory, validation on, would report "some resources are still bound to the
    /// memory" and leave the allocation unfreed), the device still counts every allocation, and a
    /// copy through both buffers still reads back. Disposing the placed buffer drops the first count
    /// to 1; once every resource is gone each allocation disposes. Then, on D3D12, a new device works:
    /// memory freed with a resource bound makes every later D3D12CreateDevice in the process fail
    /// with 0x887A0007 (measured in probes only; CLAUDE.md: Lifetime).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Memory_refuses_disposal_while_resources_are_bound(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        List<NriMessage> Since(int start) { lock (messages) return messages.Skip(start).ToList(); }

        using (var device = TestDevice.Create(backend.Api, messages))
        {
            var resources = new List<NriObject>();
            var memories = new List<NriMemory>();
            T Resource<T>(T resource) where T : NriObject { resources.Add(resource); return resource; }
            NriMemory Memory(NriMemory memory) { memories.Add(memory); return memory; }
            try
            {
                var boundBuffer = Resource(device.CreateBuffer(StorageBuffer));
                var bufferRequirements = boundBuffer.GetMemoryDesc(MemoryLocation.DEVICE);
                Assert.True(bufferRequirements.mustBeDedicated == 0, $"{backend}: the buffer needs a dedicated allocation, so a second cannot be placed beside it");
                var stride = Align(bufferRequirements.size, Math.Max(bufferRequirements.alignment, 1u));
                var bufferMemory = Memory(device.AllocateMemory(new AllocateMemoryDesc { size = stride * 2, type = bufferRequirements.type }));
                device.BindBufferMemory(boundBuffer, bufferMemory);
                var placedBuffer = Resource(device.CreatePlacedBuffer(bufferMemory, stride, StorageBuffer));

                var boundTexture = Resource(device.CreateTexture(SampledTexture));
                var textureRequirements = boundTexture.GetMemoryDesc(MemoryLocation.DEVICE);
                var textureMemory = Memory(device.AllocateMemory(AllocationFor(textureRequirements)));
                device.BindTextureMemory(boundTexture, textureMemory);
                var placedTextureMemory = Memory(device.AllocateMemory(AllocationFor(textureRequirements)));
                var placedTexture = Resource(device.CreatePlacedTexture(placedTextureMemory, 0, SampledTexture));

                // The counts first: a wrong one would let Dispose free bound memory in this process.
                Assert.Equal((2, 1, 1), (bufferMemory.BoundResources, textureMemory.BoundResources, placedTextureMemory.BoundResources));
                int start;
                lock (messages) start = messages.Count;
                var live = device.LiveObjects;
                foreach (var (memory, count) in new[] { (bufferMemory, 2), (textureMemory, 1), (placedTextureMemory, 1) })
                {
                    var refused = Assert.Throws<InvalidOperationException>(memory.Dispose);
                    Record($"{backend}: {refused.Message}");
                    Assert.Contains($"with {count} resource(s) still bound", refused.Message, StringComparison.Ordinal);
                    Assert.False(memory.IsDisposed);
                }
                Assert.Equal(live, device.LiveObjects);
                Assert.Empty(Since(start));

                // The allocation still backs both buffers: a copy through them reads back.
                using (var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = StorageBuffer.size }))
                using (var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = StorageBuffer.size }))
                {
                    upload.Map().Fill(0x5C);
                    upload.Unmap();
                    Run(device, cmd =>
                    {
                        cmd.Barrier([], [new NriBufferBarrier(upload, Nothing, CopySource), new NriBufferBarrier(boundBuffer, Nothing, CopyDestination), new NriBufferBarrier(placedBuffer, Nothing, CopyDestination)], []);
                        cmd.CopyBuffer(boundBuffer, 0, upload, 0, StorageBuffer.size);
                        cmd.Barrier(new NriBufferBarrier(boundBuffer, CopyDestination, CopySource));
                        cmd.CopyBuffer(placedBuffer, 0, boundBuffer, 0, StorageBuffer.size);
                        cmd.Barrier(new NriBufferBarrier(placedBuffer, CopyDestination, CopySource));
                        cmd.CopyBuffer(readback, 0, placedBuffer, 0, StorageBuffer.size);
                    });
                    var bytes = readback.Map().ToArray();
                    readback.Unmap();
                    Assert.True(bytes.All(b => b == 0x5C), $"readback starts {Convert.ToHexString(bytes, 0, 16)}, expected 5C throughout");
                }

                placedBuffer.Dispose();
                var stillBound = Assert.Throws<InvalidOperationException>(bufferMemory.Dispose);
                Assert.Contains("with 1 resource(s) still bound", stillBound.Message, StringComparison.Ordinal);
                boundBuffer.Dispose();
                bufferMemory.Dispose();
                boundTexture.Dispose();
                textureMemory.Dispose();
                placedTexture.Dispose();
                placedTextureMemory.Dispose();
                Assert.All(memories, m => Assert.True(m.IsDisposed));
                Assert.Equal(0, device.LiveObjects);
            }
            finally
            {
                for (var i = resources.Count - 1; i >= 0; i--) resources[i].Dispose();
                foreach (var memory in memories) memory.Dispose();
            }
        }
        Assert.DoesNotContain(messages, m => m.IsError);

        if (backend.Api != GraphicsAPI.D3D12) return;
        var next = new List<NriMessage>();
        using (var device = TestDevice.Create(GraphicsAPI.D3D12, next))
        using (var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = StorageBuffer.size }))
        {
            Assert.NotEqual(0UL, buffer.GetNativeObject());
        }
        Assert.DoesNotContain(next, m => m.IsError);
    }
}
