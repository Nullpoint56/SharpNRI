// NriPipeline and NriPipelineCache: a compute pipeline from Shaders/Fill.hlsl dispatched once,
// writing a known pattern into a storage buffer that is copied out and checked; and a pipeline
// cache round trip (empty cache -> pipeline -> GetData -> second cache primed with the bytes).

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class PipelineTests
{
    private const int ThreadCount = 64; // [numthreads(64, 1, 1)] in Fill.hlsl
    private const uint Pattern = 0xC0FFEE00u;

    /// <summary>SPIR-V on Vulkan, DXIL on D3D12.</summary>
    internal static byte[] Bytecode(NriDevice device, string name)
    {
        var extension = device.GraphicsApi == GraphicsAPI.D3D12 ? "dxil" : "spv";
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", $"{name}.{extension}"));
    }

    /// <summary>Set 0, space 0: one STORAGE_STRUCTURED_BUFFER at u0, compute only.</summary>
    private static NriPipelineLayoutDesc FillLayout() => new()
    {
        DescriptorSets =
        [
            new NriDescriptorSetDesc(0,
            [
                new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
            ]),
        ],
        ShaderStages = StageBits.COMPUTE_SHADER,
    };

    private static NriPipeline CreateFill(NriDevice device, NriPipelineLayout layout, NriPipelineCache? cache = null) =>
        device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, Bytecode(device, "Fill.cs"), "main"),
            Cache = cache,
        });

    /// <summary>
    /// A pipeline layout whose ShaderStages is ALL (0, NRI's "lazy default" for barriers) takes a
    /// range with explicit stages, and compute and graphics pipelines create on it (patch 0016).
    /// The D3D12 backend reads ALL on the layout as every shader stage; upstream validation masked
    /// the range's stages with 0 and refused the layout ("not compatible with 'shaderStages'"), and
    /// a graphics pipeline on a layout whose ranges are all ALL as "not enabled in the pipeline
    /// layout", so the lazy default could not be used on a layout at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_layout_with_ShaderStages_ALL_takes_explicit_range_stages_and_pipelines(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var spirv = backend.Api == GraphicsAPI.VK;

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets =
            [
                new NriDescriptorSetDesc(0,
                [
                    new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
                ]),
            ],
            ShaderStages = StageBits.ALL,
        });
        using var compute = CreateFill(device, layout);
        using var graphics = device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            InputAssembly = new InputAssemblyDesc { topology = Topology.TRIANGLE_LIST },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE },
            OutputMerger = new NriOutputMergerDesc { Colors = [new ColorAttachmentDesc { format = Format.RGBA8_UNORM, colorWriteMask = ColorWriteBits.RGBA }] },
            Shaders =
            [
                new NriShaderDesc(StageBits.VERTEX_SHADER, Bytecode(device, "Triangle.vs"), "vsMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, Bytecode(device, "Triangle.ps"), "psMain"),
            ],
        });
        Assert.Equal(BindPoint.COMPUTE, compute.BindPoint);
        Assert.Equal(BindPoint.GRAPHICS, graphics.BindPoint);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Compute_dispatch_writes_a_known_pattern_into_a_storage_buffer(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.True(backend.Api == GraphicsAPI.D3D12 ? device.Desc.features.shaderBytecodeDXIL != 0 : device.Desc.features.shaderBytecodeSPIRV != 0);

        const ulong size = ThreadCount * sizeof(uint);
        using var layout = device.CreatePipelineLayout(FillLayout());
        using var pipeline = CreateFill(device, layout);
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 1 });
        using var output = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = size, structureStride = sizeof(uint), usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var view = output.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = size });

        var set = pool.AllocateDescriptorSet(layout, 0);
        set.Update(0, 0, view);

        Assert.Equal(BindPoint.COMPUTE, pipeline.BindPoint);

        Run(device, cmd =>
        {
            cmd.Barrier(new NriBufferBarrier(output, Nothing, ComputeStorage));
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.Dispatch(1);
            cmd.Barrier(new NriBufferBarrier(output, ComputeStorage, CopySource));
            cmd.CopyBuffer(readback, 0, output, 0, size);
        }, pool);

        var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(readback.Map()).ToArray();
        readback.Unmap();

        Assert.Equal(ThreadCount, words.Length);
        for (var i = 0; i < ThreadCount; i++) Assert.Equal(Pattern + (uint)i, words[i]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Pipeline_cache_round_trips_through_GetData(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Record($"{backend}: features.pipelineCache = {device.Desc.features.pipelineCache}");

        using var layout = device.CreatePipelineLayout(FillLayout());

        byte[] data;
        using (var empty = device.CreatePipelineCache())
        using (var pipeline = CreateFill(device, layout, empty))
        {
            data = empty.GetData();
        }
        Record($"{backend}: GetData after one compute pipeline returned {data.Length} bytes");

        using var primed = device.CreatePipelineCache(data);
        using var second = CreateFill(device, layout, primed);
        var again = primed.GetData();
        Record($"{backend}: the primed cache returned {again.Length} bytes after a second, identical pipeline");

        // Measured on the RTX 4070 SUPER: VK returns 6872 bytes (VkPipelineCache header plus the
        // compiled pipeline), D3D12 3318 bytes (an ID3D12PipelineLibrary serialization); the
        // primed cache returns the same size after the identical second pipeline, i.e. it was a
        // hit. NRI.h allows an empty result, so the sizes are not pinned, only "non-empty and kept".
        Assert.NotEmpty(data);
        Assert.True(again.Length >= data.Length, $"primed {again.Length} < original {data.Length}");
        Assert.Equal(3, device.LiveObjects); // layout, primed cache, second pipeline
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // Stale or foreign cache bytes. NRI returns OUT_OF_DATE from CreatePipelineCache and, in
    // CreateImplementation (DeviceVK.h, DeviceD3D12.h), destroys the half-made object and nulls the
    // out handle; the validation layer does the same (DeviceVal.hpp). So nothing is handed back to
    // leak: the binding throws, the host allocator balances and the device disposes clean.
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_cache_primed_with_garbage_is_refused_with_OUT_OF_DATE_and_leaks_nothing(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        var device = TestDevice.Create(backend.Api, messages, b => b.Allocator = allocator);
        Assert.SkipWhen(device.Desc.features.pipelineCache == 0, $"{backend}: features.pipelineCache = 0; the backend ignores cache data");
        try
        {
            var garbage = new byte[4096];
            new Random(0x5a1e).NextBytes(garbage);
            var live = allocator.Live;

            var e = Assert.Throws<NriException>(() => device.CreatePipelineCache(garbage));
            Record($"{backend}: CreatePipelineCache(4096 random bytes) -> {e.Result}; host allocations live before {live}, after {allocator.Live}");

            Assert.Equal(Result.OUT_OF_DATE, e.Result);
            Assert.Equal(live, allocator.Live);
            Assert.Equal(0, device.LiveObjects);
        }
        finally
        {
            device.Dispose();
        }
        Record($"{backend}: after disposal {allocator.Allocations} allocations, {allocator.Frees} frees, Live = {allocator.Live}");
        Assert.Equal(0L, allocator.Live);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Compute_pipeline_is_counted_and_disposed(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var layout = device.CreatePipelineLayout(FillLayout());
        var pipeline = CreateFill(device, layout);
        Assert.Equal(2, device.LiveObjects);
        pipeline.Dispose();
        Assert.Equal(1, device.LiveObjects);
        Assert.True(pipeline.IsDisposed);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
