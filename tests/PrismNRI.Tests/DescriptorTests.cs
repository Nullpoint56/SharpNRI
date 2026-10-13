// Samplers, descriptor pools, a pipeline layout with one set of three ranges and a root constant,
// descriptor sets allocated from the pool, written, copied, located in the heaps, and reset.
//
// Register assignment: the device has all-zero VKBindingOffsets, so on Vulkan a range's binding is
// its register index regardless of class. b0, t1-t2 and s3 keep the three ranges on distinct
// bindings; the root constant lives in its own register space (1), which Vulkan turns into a push
// constant range and D3D12 into root constants.

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class DescriptorTests
{
    private const uint ConstantBufferRange = 0, TextureRange = 1, SamplerRange = 2;
    private const uint TexturesPerSet = 2;

    private static NriPipelineLayoutDesc LayoutDesc() => new()
    {
        RootRegisterSpace = 1,
        RootConstants = [new RootConstantDesc { registerIndex = 0, size = 16, shaderStages = StageBits.ALL }],
        DescriptorSets =
        [
            new NriDescriptorSetDesc(0,
            [
                new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.ALL },
                new DescriptorRangeDesc { baseRegisterIndex = 1, descriptorNum = TexturesPerSet, descriptorType = DescriptorType.TEXTURE, shaderStages = StageBits.ALL },
                new DescriptorRangeDesc { baseRegisterIndex = 3, descriptorNum = 1, descriptorType = DescriptorType.SAMPLER, shaderStages = StageBits.ALL },
            ]),
        ],
        ShaderStages = StageBits.ALL,
    };

    private static DescriptorPoolDesc PoolDesc(uint sets, DescriptorPoolBits flags = DescriptorPoolBits.NONE) => new()
    {
        descriptorSetMaxNum = sets,
        samplerMaxNum = sets,
        constantBufferMaxNum = sets,
        textureMaxNum = sets * TexturesPerSet,
        storageTextureMaxNum = 2,
        bufferMaxNum = 2,
        storageBufferMaxNum = 2,
        structuredBufferMaxNum = 2,
        storageStructuredBufferMaxNum = 2,
        flags = flags,
    };

    private static TextureDesc SampledTexture => new()
    {
        type = TextureType.TEXTURE_2D,
        usage = TextureUsageBits.SHADER_RESOURCE,
        format = Format.RGBA8_UNORM,
        width = 4,
        height = 4,
        depth = 1,
        mipNum = 1,
        layerNum = 1,
        sampleNum = 1,
    };

    /// <summary>The descriptors one set's three ranges are filled with.</summary>
    private sealed class Contents : IDisposable
    {
        public readonly NriBuffer Buffer;
        public readonly NriDescriptor ConstantView;
        public readonly NriTexture Texture;
        public readonly NriDescriptor[] TextureViews;
        public readonly NriDescriptor Sampler;

        public Contents(NriDevice device)
        {
            Buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.CONSTANT });
            ConstantView = Buffer.CreateView(BufferView.CONSTANT_BUFFER, 0, 256);
            Texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, SampledTexture);
            TextureViews = [Texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM), Texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM)];
            Sampler = device.CreateSampler(new SamplerDesc());
        }

        public void WriteInto(NriDescriptorSet set)
        {
            set.Update(ConstantBufferRange, 0, ConstantView);
            set.UpdateRange(TextureRange, 0, TextureViews);
            set.Update(SamplerRange, 0, Sampler);
        }

        public void Dispose()
        {
            Sampler.Dispose();
            foreach (var v in TextureViews.Reverse()) v.Dispose();
            Texture.Dispose();
            ConstantView.Dispose();
            Buffer.Dispose();
        }
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Sampler_is_created_from_a_default_desc(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var sampler = device.CreateSampler(new SamplerDesc());

        Assert.Equal(NriDescriptorKind.Sampler, sampler.Kind);
        Assert.Equal(1, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Sampler_is_created_from_a_filled_desc(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var desc = new SamplerDesc
        {
            filters = new Filters { min = Filter.LINEAR, mag = Filter.LINEAR, mip = Filter.LINEAR, op = FilterOp.AVERAGE },
            anisotropy = 8,
            mipBias = 0.5f,
            mipMin = 0,
            mipMax = 12,
            addressModes = new AddressModes { u = AddressMode.CLAMP_TO_EDGE, v = AddressMode.MIRRORED_REPEAT, w = AddressMode.CLAMP_TO_BORDER },
            compareOp = CompareOp.LESS_EQUAL,
            borderColor = new Color { f = new Color32f { x = 0, y = 0, z = 0, w = 1 } },
        };
        using var sampler = device.CreateSampler(desc);

        Assert.Equal(NriDescriptorKind.Sampler, sampler.Kind);
        Assert.NotEqual(0UL, sampler.GetNativeObject());
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Pool_is_created_with_room_for_a_few_of_each_type(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var pool = device.CreateDescriptorPool(PoolDesc(4));
        using var copySource = device.CreateDescriptorPool(PoolDesc(1, DescriptorPoolBits.COPY_SOURCE));

        Assert.Equal(2, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Pipeline_layout_holds_three_ranges_and_a_root_constant(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // NRIDescs.h, RootConstantDesc.size: "must be non-zero and a multiple of 4".
        Assert.True(device.Desc.pipelineLayout.rootConstantMaxSize >= 16, $"rootConstantMaxSize {device.Desc.pipelineLayout.rootConstantMaxSize}");

        var desc = LayoutDesc();
        using var layout = device.CreatePipelineLayout(desc);

        Assert.Same(desc, layout.Desc);
        Assert.Equal(1, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Several_sets_are_allocated_and_written_with_a_buffer_view_texture_views_and_a_sampler(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var layout = device.CreatePipelineLayout(LayoutDesc());
        using var pool = device.CreateDescriptorPool(PoolDesc(4));
        using var contents = new Contents(device);

        var sets = pool.AllocateDescriptorSets(layout, 0, 3);
        Assert.Equal(3, sets.Length);
        Assert.All(sets, s => Assert.Same(pool, s.Pool));

        foreach (var set in sets) contents.WriteInto(set);

        // A set written outside a command buffer is bindable in one; bind it to prove it.
        Run(device, cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetDescriptorSet(0, sets[1], BindPoint.COMPUTE);
            cmd.SetRootConstants(0, new System.Numerics.Vector4(1, 2, 3, 4), 0, BindPoint.COMPUTE);
        }, pool);

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void CopyRange_copies_from_a_COPY_SOURCE_pool(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var layout = device.CreatePipelineLayout(LayoutDesc());
        using var sourcePool = device.CreateDescriptorPool(PoolDesc(1, DescriptorPoolBits.COPY_SOURCE));
        using var pool = device.CreateDescriptorPool(PoolDesc(1));
        using var contents = new Contents(device);

        var source = sourcePool.AllocateDescriptorSet(layout, 0);
        contents.WriteInto(source);

        var destination = pool.AllocateDescriptorSet(layout, 0);
        destination.CopyRange(ConstantBufferRange, 0, source, ConstantBufferRange, 0, 1);
        destination.CopyRange(TextureRange, 0, source, TextureRange, 0, TexturesPerSet);
        destination.CopyRange(SamplerRange, 0, source, SamplerRange, 0, 1);

        // The copy is bindable; the COPY_SOURCE set itself is not (NRIDescs.h: "such sets can't be bound").
        Run(device, cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetDescriptorSet(0, destination, BindPoint.COMPUTE);
        }, pool);

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void GetOffsets_is_zero_on_Vulkan_and_locates_each_set_in_the_D3D12_heaps(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var layout = device.CreatePipelineLayout(LayoutDesc());
        using var pool = device.CreateDescriptorPool(PoolDesc(4));
        var sets = pool.AllocateDescriptorSets(layout, 0, 3);
        var offsets = sets.Select(s => s.GetOffsets()).ToArray();
        Record($"{backend}: GetDescriptorSetOffsets for 3 consecutive sets = {string.Join(", ", offsets.Select(o => $"({o.ResourceHeap},{o.SamplerHeap})"))}");

        switch (backend.Api)
        {
            case GraphicsAPI.D3D12:
                // Measured: sets are laid out back to back in the pool's heaps; each set takes one
                // CBV plus two SRVs (3) in the resource heap and one sampler in the sampler heap.
                Assert.Equal((0u, 0u), offsets[0]);
                Assert.Equal((3u, 1u), offsets[1]);
                Assert.Equal((6u, 2u), offsets[2]);
                break;
            default:
                // NRI.h: "VK: GetDescriptorSetOffsets returns 0".
                Assert.All(offsets, o => Assert.Equal((0u, 0u), o));
                break;
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Reset_returns_the_whole_pool(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var layout = device.CreatePipelineLayout(LayoutDesc());
        using var pool = device.CreateDescriptorPool(PoolDesc(4));

        // Fill the pool to capacity, reset, and fill it again: without the reset the second
        // allocation would exceed descriptorSetMaxNum.
        Assert.Equal(4, pool.AllocateDescriptorSets(layout, 0, 4).Length);
        pool.Reset();
        var again = pool.AllocateDescriptorSets(layout, 0, 4);
        Assert.Equal(4, again.Length);

        if (backend.Api == GraphicsAPI.D3D12) Assert.Equal((0u, 0u), again[0].GetOffsets());
        Assert.Equal(2, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
