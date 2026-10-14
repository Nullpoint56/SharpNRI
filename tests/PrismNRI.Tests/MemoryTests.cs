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
}
