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
