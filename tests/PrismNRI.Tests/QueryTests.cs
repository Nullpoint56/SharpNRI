// NriQueryPool: timestamps written around a trivial piece of GPU work, copied into a readback
// buffer, and ordered; the size of one timestamp result; occlusion queries around a visible and a
// hidden draw, reused after a command-buffer and after a host reset; pipeline statistics of a draw
// and of a dispatch, read through PipelineStatisticsDesc; and calibrated timestamps around a GPU
// timestamp.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class QueryTests
{
    private const ushort Size = 16; // QueryQuad.hlsl covers the whole viewport: Size * Size pixels, one sample each
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Timestamp_query_size_is_8_bytes(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, 2);

        Assert.Equal(QueryType.TIMESTAMP, pool.Type);
        Assert.Equal(2u, pool.Capacity);

        // NRIDescs.h: TIMESTAMP is "uint64_t".
        Assert.Equal(8u, pool.QuerySize);
        Assert.Equal(1, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Two_timestamps_around_GPU_work_are_ordered(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.timestamp == 0, $"{backend}: features.timestamp is false");

        using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, 2);
        var querySize = pool.QuerySize;
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 2 * querySize });
        using var source = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 65536 });
        using var destination = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 65536 });

        Run(device, cmd =>
        {
            cmd.ResetQueries(pool, 0, 2);
            cmd.EndQuery(pool, 0);
            cmd.CopyBuffer(destination, 0, source, 0, 65536);
            cmd.EndQuery(pool, 1);
            cmd.CopyQueries(pool, 0, 2, readback, 0);
        });

        var ticks = MemoryMarshal.Cast<byte, ulong>(readback.Map()).ToArray();
        readback.Unmap();

        var frequency = device.Desc.other.timestampFrequencyHz;
        Record($"{backend}: timestamps {ticks[0]} -> {ticks[1]} ({ticks[1] - ticks[0]} ticks at {frequency} Hz = {(ticks[1] - ticks[0]) * 1e9 / frequency:F0} ns)");

        // MoltenVK on the paravirtual Apple GPU reports timestampValidBits and writes zeros when
        // the copy is recorded in the same submission as the writes (CLAUDE.md: MoltenVK and the paravirtual Mac). Measured here
        // rather than assumed: the same queries copied again by a later submission are recorded,
        // and the test skips only while the in-submission copy still reads zero there.
        if (TestDevice.IsAppleParavirtualVK(backend.Api) && ticks[0] == 0 && ticks[1] == 0)
        {
            Run(device, cmd => cmd.CopyQueries(pool, 0, 2, readback, 0));
            var later = MemoryMarshal.Cast<byte, ulong>(readback.Map()).ToArray();
            readback.Unmap();
            Record($"{backend}: the same queries copied by a later submission: {later[0]} -> {later[1]}");
            Assert.Skip($"MoltenVK on the Apple Paravirtual device writes 0 for both timestamps when they are copied in the submission that "
                + $"wrote them; a later submission copies {later[0]} -> {later[1]}");
        }

        Assert.NotEqual(0UL, ticks[0]);
        Assert.True(ticks[1] >= ticks[0], $"second timestamp {ticks[1]} < first {ticks[0]}");
        Assert.NotEqual(0UL, frequency);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Host_reset_of_an_idle_pool_is_accepted(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, 4);

        pool.Reset(0, 4);

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Occlusion queries (features.occlusion) around two draws of QueryQuad.hlsl over a depth buffer
    /// cleared to 0.5 with test LESS: the viewport's depth range puts the quad at 0.25 (in front, every
    /// sample passes) or at 0.75 (behind, none does). The results go through CmdCopyQueries. Then
    /// the pool is reused twice, each time with the two draws in the other order, reset first by
    /// CmdResetQueries and then by the host (NriQueryPool.Reset), and each query reports its new
    /// draw, not the old one.
    /// D3D12's OCCLUSION query counts samples exactly; NRI begins Vulkan occlusion queries without
    /// VK_QUERY_CONTROL_PRECISE_BIT (CommandBufferVK::BeginQuery), and then Vulkan guarantees only a
    /// non-zero result when samples pass, so on Vulkan the visible count is asserted non-zero and
    /// recorded.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Occlusion_counts_the_samples_in_front_of_the_depth_buffer_and_none_behind(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.occlusion == 0, $"{backend}: features.occlusion is false");
        Assert.SkipWhen((device.GetFormatSupport(Format.D32_SFLOAT) & FormatSupportBits.DEPTH_STENCIL_ATTACHMENT) == 0, $"{backend}: D32_SFLOAT lacks DEPTH_STENCIL_ATTACHMENT");

        using var color = CreateTarget(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT);
        using var colorView = color.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var depth = CreateTarget(device, Format.D32_SFLOAT, TextureUsageBits.DEPTH_STENCIL_ATTACHMENT);
        using var depthView = depth.CreateView(TextureView.DEPTH_STENCIL_ATTACHMENT, Format.D32_SFLOAT);
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
        using var pipeline = CreateQuadPipeline(device, layout, new DepthAttachmentDesc { compareOp = CompareOp.LESS, write = 0 }, Format.D32_SFLOAT);
        using var pool = device.CreateQueryPool(QueryType.OCCLUSION, 2);
        Assert.Equal(8u, pool.QuerySize); // NRIDescs.h: OCCLUSION is "uint64_t"
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 2 * pool.QuerySize });

        var depthClear = new ClearValue { depthStencil = new DepthStencil { depth = 0.5f } };
        var inFront = new Viewport { width = Size, height = Size, depthMin = 0.0f, depthMax = 0.5f };
        var behind = new Viewport { width = Size, height = Size, depthMin = 0.5f, depthMax = 1.0f };
        var depthAttachment = new AccessLayoutStage { access = AccessBits.DEPTH_STENCIL_ATTACHMENT, layout = Layout.DEPTH_STENCIL_ATTACHMENT, stages = StageBits.DEPTH_STENCIL_ATTACHMENT };

        ulong[] Round(Viewport first, Viewport second, bool resetOnTheGpu)
        {
            Run(device, cmd =>
            {
                if (resetOnTheGpu) cmd.ResetQueries(pool, 0, 2);
                cmd.Barrier([], [], [new NriTextureBarrier(color, Undefined, ColorAttachment), new NriTextureBarrier(depth, Undefined, depthAttachment)]);
                cmd.BeginRendering([new NriAttachment(colorView, default, LoadOp.CLEAR, StoreOp.STORE)], new NriAttachment(depthView, depthClear, LoadOp.CLEAR, StoreOp.STORE));
                cmd.SetScissor(new Rect { width = Size, height = Size });
                cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
                cmd.SetPipeline(pipeline);
                cmd.BeginQuery(pool, 0);
                cmd.SetViewport(first);
                cmd.Draw(6);
                cmd.EndQuery(pool, 0);
                cmd.BeginQuery(pool, 1);
                cmd.SetViewport(second);
                cmd.Draw(6);
                cmd.EndQuery(pool, 1);
                cmd.EndRendering();
                cmd.CopyQueries(pool, 0, 2, readback, 0);
            });
            var results = MemoryMarshal.Cast<byte, ulong>(readback.Map()).ToArray();
            readback.Unmap();
            return results;
        }

        var first = Round(inFront, behind, resetOnTheGpu: true);
        var swapped = Round(behind, inFront, resetOnTheGpu: true);
        pool.Reset(0, 2);
        var hostReset = Round(inFront, behind, resetOnTheGpu: false);
        Record($"{backend}: samples passed (in front, behind): ({first[0]}, {first[1]}); swapped after CmdResetQueries: ({swapped[1]}, {swapped[0]}); after the host reset: ({hostReset[0]}, {hostReset[1]}); {Size * Size} samples drawn");

        foreach (var (visible, hidden, what) in new[] { (first[0], first[1], "first use"), (swapped[1], swapped[0], "reuse after CmdResetQueries"), (hostReset[0], hostReset[1], "reuse after the host reset") })
        {
            Assert.True(hidden == 0, $"{what}: {hidden} samples passed behind the depth buffer");
            if (backend.Api == GraphicsAPI.D3D12) Assert.True(visible == Size * Size, $"{what}: {visible} samples passed in front, expected {Size * Size}");
            else Assert.True(visible != 0, $"{what}: no sample passed in front of the depth buffer");
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Pipeline statistics (features.pipelineStatistics) through PipelineStatisticsDesc: a draw of
    /// two triangles (six vertices, no index buffer) covering a 16x16 target counts six input
    /// vertices, two input primitives, at least six vertex shader invocations, two primitives into
    /// and out of the rasterizer, and at least one fragment shader invocation per pixel; a dispatch
    /// of Fill.hlsl, four groups of 64, counts 256 compute invocations. Each query counts only its
    /// own work, which places every counter in its PipelineStatisticsDesc field on both backends.
    /// Results are QuerySize bytes apart, not sizeof(PipelineStatisticsDesc): 13 counters on Vulkan
    /// when NRI enables mesh shaders, 14 on D3D12 with PIPELINE_STATISTICS1
    /// (features.meshShaderPipelineStats), 11 otherwise (QueryPoolVK.hpp, QueryPoolD3D12.hpp).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Pipeline_statistics_count_a_draw_and_a_dispatch_in_their_own_fields(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.pipelineStatistics == 0, $"{backend}: features.pipelineStatistics is false");

        using var color = CreateTarget(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT);
        using var colorView = color.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var graphicsLayout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
        using var quad = CreateQuadPipeline(device, graphicsLayout, default, Format.UNKNOWN);

        const uint groups = 4, threadsPerGroup = 64; // [numthreads(64, 1, 1)] in Fill.hlsl
        using var computeLayout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER }])],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var fill = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = computeLayout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "Fill.cs"), "main"),
        });
        using var descriptorPool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 1 });
        using var output = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = groups * threadsPerGroup * sizeof(uint), structureStride = sizeof(uint), usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var outputView = output.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
        var set = descriptorPool.AllocateDescriptorSet(computeLayout, 0);
        set.Update(0, 0, outputView);

        using var pool = device.CreateQueryPool(QueryType.PIPELINE_STATISTICS, 2);
        var querySize = pool.QuerySize;
        var descSize = (uint)Marshal.SizeOf<PipelineStatisticsDesc>();
        Record($"{backend}: PIPELINE_STATISTICS query size {querySize} bytes ({querySize / 8} counters); PipelineStatisticsDesc is {descSize}; features.meshShaderPipelineStats = {device.Desc.features.meshShaderPipelineStats}");
        Assert.True(querySize % 8 == 0 && querySize >= 11 * 8 && querySize <= descSize, $"query size {querySize}");
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 2 * querySize });

        Run(device, cmd =>
        {
            cmd.ResetQueries(pool, 0, 2);
            cmd.Barrier([], [new NriBufferBarrier(output, Nothing, ComputeStorage)], [new NriTextureBarrier(color, Undefined, ColorAttachment)]);
            cmd.BeginRendering([new NriAttachment(colorView, default, LoadOp.CLEAR, StoreOp.STORE)]);
            cmd.SetViewport(new Viewport { width = Size, height = Size, depthMax = 1 });
            cmd.SetScissor(new Rect { width = Size, height = Size });
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, graphicsLayout);
            cmd.SetPipeline(quad);
            cmd.BeginQuery(pool, 0);
            cmd.Draw(6);
            cmd.EndQuery(pool, 0);
            cmd.EndRendering();
            cmd.SetPipelineLayout(BindPoint.COMPUTE, computeLayout);
            cmd.SetPipeline(fill);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.BeginQuery(pool, 1);
            cmd.Dispatch(groups);
            cmd.EndQuery(pool, 1);
            cmd.CopyQueries(pool, 0, 2, readback, 0);
        }, descriptorPool);

        var bytes = readback.Map().ToArray();
        readback.Unmap();
        PipelineStatisticsDesc Read(int query)
        {
            var desc = new PipelineStatisticsDesc();
            bytes.AsSpan((int)(query * querySize), (int)querySize).CopyTo(MemoryMarshal.AsBytes(new Span<PipelineStatisticsDesc>(ref desc)));
            return desc;
        }
        var draw = Read(0);
        var dispatch = Read(1);
        Record($"{backend}: draw: {Describe(draw)}");
        Record($"{backend}: dispatch: {Describe(dispatch)}");

        Assert.Equal(6UL, draw.inputVertexNum);
        Assert.Equal(2UL, draw.inputPrimitiveNum);
        Assert.True(draw.vertexShaderInvocationNum >= 6, $"vertex shader invocations {draw.vertexShaderInvocationNum} < 6 vertices");
        Assert.Equal(2UL, draw.rasterizerInPrimitiveNum);
        Assert.Equal(2UL, draw.rasterizerOutPrimitiveNum);
        // At most once per pixel per triangle: helper invocations of the 2x2 quads the shared edge
        // crosses may be counted (measured: 256 on a discrete GPU, 288 = 256 + 8 quads x 4 on a
        // software rasterizer), nothing beyond.
        Assert.True(draw.fragmentShaderInvocationNum >= Size * Size, $"fragment shader invocations {draw.fragmentShaderInvocationNum} < {Size * Size} covered pixels");
        Assert.True(draw.fragmentShaderInvocationNum <= 2 * Size * Size, $"fragment shader invocations {draw.fragmentShaderInvocationNum} > 2 triangles x {Size * Size} pixels");
        Assert.Equal(0UL, draw.geometryShaderInvocationNum);
        Assert.Equal(0UL, draw.tessControlShaderInvocationNum);
        Assert.Equal(0UL, draw.tessEvaluationShaderInvocationNum);
        Assert.Equal(0UL, draw.computeShaderInvocationNum);

        Assert.Equal((ulong)(groups * threadsPerGroup), dispatch.computeShaderInvocationNum);
        Assert.Equal(0UL, dispatch.inputVertexNum);
        Assert.Equal(0UL, dispatch.vertexShaderInvocationNum);
        Assert.Equal(0UL, dispatch.fragmentShaderInvocationNum);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// QueueGetCalibratedTimestamps (features.calibratedTimestamps): two calls around a submission
    /// that writes a timestamp query, waited for. The GPU and CPU values both advance between the
    /// calls, and the query's timestamp, in the same GPU time domain, lies between the two GPU values.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Calibrated_timestamps_advance_and_bracket_a_GPU_timestamp(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.calibratedTimestamps == 0, $"{backend}: features.calibratedTimestamps is false");
        Assert.SkipWhen(device.Desc.features.timestamp == 0, $"{backend}: features.timestamp is false");
        var queue = device.GetQueue(QueueType.GRAPHICS);

        using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, 1);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = pool.QuerySize });

        var before = queue.GetCalibratedTimestamps();
        Run(device, cmd =>
        {
            cmd.ResetQueries(pool, 0, 1);
            cmd.EndQuery(pool, 0);
            cmd.CopyQueries(pool, 0, 1, readback, 0);
        });
        var after = queue.GetCalibratedTimestamps();
        var written = MemoryMarshal.Read<ulong>(readback.Map());
        readback.Unmap();

        var frequency = device.Desc.other.timestampFrequencyHz;
        Record($"{backend}: calibrated GPU {before.Gpu} -> {after.Gpu} ({after.Gpu - before.Gpu} ticks at {frequency} Hz), CPU {before.Cpu} -> {after.Cpu}; the query wrote {written}, {(long)(written - before.Gpu)} ticks after the first call");

        // The measured paravirtual-Mac defect of Two_timestamps_around_GPU_work_are_ordered: a
        // timestamp copied in the submission that wrote it reads 0 there (CLAUDE.md: MoltenVK and
        // the paravirtual Mac). Copied again by a later submission, recorded, then skipped.
        if (TestDevice.IsAppleParavirtualVK(backend.Api) && written == 0)
        {
            Run(device, cmd => cmd.CopyQueries(pool, 0, 1, readback, 0));
            var later = MemoryMarshal.Read<ulong>(readback.Map());
            readback.Unmap();
            Record($"{backend}: the same query copied by a later submission: {later}");
            Assert.Skip($"MoltenVK on the Apple Paravirtual device writes 0 for a timestamp copied in the submission that wrote it; a later submission copies {later}");
        }

        Assert.True(after.Gpu > before.Gpu, $"calibrated GPU timestamp did not advance: {before.Gpu} -> {after.Gpu}");
        Assert.True(after.Cpu > before.Cpu, $"calibrated CPU timestamp did not advance: {before.Cpu} -> {after.Cpu}");
        Assert.True(written >= before.Gpu && written <= after.Gpu, $"the query's timestamp {written} is outside [{before.Gpu}, {after.Gpu}]");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static string Describe(in PipelineStatisticsDesc s) =>
        $"IA vertices {s.inputVertexNum}, IA primitives {s.inputPrimitiveNum}, VS {s.vertexShaderInvocationNum}, GS {s.geometryShaderInvocationNum}/{s.geometryShaderPrimitiveNum}, "
        + $"rasterizer in {s.rasterizerInPrimitiveNum}, out {s.rasterizerOutPrimitiveNum}, FS {s.fragmentShaderInvocationNum}, HS {s.tessControlShaderInvocationNum}, DS {s.tessEvaluationShaderInvocationNum}, "
        + $"CS {s.computeShaderInvocationNum}, task {s.taskShaderInvocationNum}, mesh {s.meshShaderInvocationNum}/{s.meshShaderPrimitiveNum}";

    private static NriTexture CreateTarget(NriDevice device, Format format, TextureUsageBits usage) =>
        device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = usage,
            format = format,
            width = Size,
            height = Size,
            depth = 1,
            mipNum = 1,
            layerNum = 1,
            sampleNum = 1,
        });

    private static NriPipeline CreateQuadPipeline(NriDevice device, NriPipelineLayout layout, DepthAttachmentDesc depth, Format depthFormat) =>
        device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            InputAssembly = new InputAssemblyDesc { topology = Topology.TRIANGLE_LIST },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE },
            OutputMerger = new NriOutputMergerDesc
            {
                Colors = [new ColorAttachmentDesc { format = Format.RGBA8_UNORM, colorWriteMask = ColorWriteBits.RGBA }],
                Depth = depth,
                DepthStencilFormat = depthFormat,
            },
            Shaders =
            [
                new NriShaderDesc(StageBits.VERTEX_SHADER, PipelineTests.Bytecode(device, "QueryQuad.vs"), "vsMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, PipelineTests.Bytecode(device, "QueryQuad.ps"), "psMain"),
            ],
        });
}
