// Indirect execution: CmdDrawIndirect and CmdDrawIndexedIndirect from argument buffers, with and
// without a count buffer, arguments a compute dispatch writes, and CmdDispatchIndirect. The records
// are the generated DrawDesc, DrawIndexedDesc and DispatchDesc structs written byte for byte, so the
// GPU's reading of them also holds their layouts. Each test starts its records past a decoy that
// draws (or dispatches) something else, so an ignored offset shows.
//
// Rendering goes through DrawTests' harness: four quads, one per quadrant, coloured red, green,
// blue, yellow, and the whole 64x64 image compared with the one it must be.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;
using static SharpNRI.Tests.DrawTests;
using static SharpNRI.Tests.GpuWork;

namespace SharpNRI.Tests;

public sealed class IndirectTests
{
    private static readonly uint DrawStride = (uint)Unsafe.SizeOf<DrawDesc>();

    /// <summary>
    /// Three DrawDesc records: a decoy for quad 0 at offset 0, then quads 1 and 3 (vertices 6-11
    /// and 18-23 of the four quads' triangle lists). DrawIndirect(offset 16, drawNum 2, stride
    /// sizeof(DrawDesc)) draws quads 1 and 3 and nothing else.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void DrawIndirect_draws_each_record_of_the_argument_buffer(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultiDraw(device);
        Assert.Equal(16u, DrawStride);

        using var scene = new TriangleScene(device);
        using var arguments = Upload(device, new[] { Draw(0), Draw(1), Draw(3) }, BufferUsageBits.ARGUMENT);
        using var canvas = new Canvas(device);
        var image = canvas.Render(cmd =>
        {
            scene.Bind(cmd);
            cmd.DrawIndirect(arguments, DrawStride, 2, DrawStride);
        })[0];

        image.ExpectQuadrants([null, Green, null, Yellow], "DrawIndirect(offset 16, drawNum 2)");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Two DrawIndexedDesc records, each padded to a 24-byte stride with a zero word, over the
    /// indexed quads (vertices 0-3, 4-7, 8-11, 12-15) and a 32-bit index buffer of the 4-7 and 8-11
    /// blocks: {baseIndex 0, baseVertex +8} draws quad 3, {baseIndex 6, baseVertex -4} quad 1. A
    /// stride read as sizeof(DrawIndexedDesc) would take the padding for the second record's
    /// indexNum and draw nothing for it.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void DrawIndexedIndirect_reads_records_at_the_given_stride(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultiDraw(device);
        Assert.Equal(20, Unsafe.SizeOf<DrawIndexedDesc>());

        using var scene = new IndexedScene(device);
        using var arguments = Upload(device, IndexedRecords, BufferUsageBits.ARGUMENT);
        using var canvas = new Canvas(device);
        var image = canvas.Render(cmd =>
        {
            scene.Bind(cmd);
            cmd.DrawIndexedIndirect(arguments, 0, 2, PaddedDrawIndexed.Stride);
        })[0];

        image.ExpectQuadrants([null, Green, null, Yellow], "DrawIndexedIndirect(drawNum 2, stride 24)");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A count buffer of three words, 99, 1, 5: read at offset 4 it limits drawNum 2 to one draw,
    /// for DrawIndirect (quad 1 only, not quad 3) and DrawIndexedIndirect (quad 3 only, not quad
    /// 1); read at offset 8, 5 is limited by drawNum 2, and both of DrawIndirect's records draw. An
    /// ignored countBufferOffset reads 99, which drawNum limits to two draws.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_count_buffer_limits_the_draws_to_min_of_count_and_drawNum(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultiDraw(device);
        Assert.SkipWhen(device.Desc.features.drawIndirectCount == 0, $"{backend}: features.drawIndirectCount = 0");

        using var triangles = new TriangleScene(device);
        using var indexed = new IndexedScene(device);
        using var drawArguments = Upload(device, new[] { Draw(0), Draw(1), Draw(3) }, BufferUsageBits.ARGUMENT);
        using var indexedArguments = Upload(device, IndexedRecords, BufferUsageBits.ARGUMENT);
        using var counts = Upload(device, new uint[] { 99, 1, 5 }, BufferUsageBits.ARGUMENT);
        using var canvas = new Canvas(device);

        var drawCountOne = canvas.Render(cmd =>
        {
            triangles.Bind(cmd);
            cmd.DrawIndirect(drawArguments, DrawStride, 2, DrawStride, counts, 4);
        })[0];
        var drawCountFive = canvas.Render(cmd =>
        {
            triangles.Bind(cmd);
            cmd.DrawIndirect(drawArguments, DrawStride, 2, DrawStride, counts, 8);
        })[0];
        var indexedCountOne = canvas.Render(cmd =>
        {
            indexed.Bind(cmd);
            cmd.DrawIndexedIndirect(indexedArguments, 0, 2, PaddedDrawIndexed.Stride, counts, 4);
        })[0];

        drawCountOne.ExpectQuadrants([null, Green, null, null], "DrawIndirect, drawNum 2, count 1");
        drawCountFive.ExpectQuadrants([null, Green, null, Yellow], "DrawIndirect, drawNum 2, count 5");
        indexedCountOne.ExpectQuadrants([null, null, null, Yellow], "DrawIndexedIndirect, drawNum 2, count 1");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The arguments come from the GPU: the buffer is zeroed, a compute dispatch
    /// (Shaders/DrawArgs.hlsl) writes two DrawDesc records into it, for quads 0 and 2, and a
    /// barrier from shader storage to ARGUMENT_BUFFER / INDIRECT makes them what DrawIndirect reads.
    /// Arguments that were not visible would be the zeros, which draw nothing: measured without the
    /// barrier, the image stays empty on a discrete GPU (Vulkan and D3D12) and no layer reports it;
    /// the next test holds the barrier on Vulkan with synchronization validation.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void DrawIndirect_draws_what_a_compute_dispatch_wrote_after_an_argument_barrier(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultiDraw(device);

        using var scene = new TriangleScene(device);
        using var computed = new ComputedArguments(device);
        using var canvas = new Canvas(device);
        var image = canvas.Render(
            before: cmd => computed.Write(cmd, barrier: true),
            inside: cmd =>
            {
                scene.Bind(cmd);
                cmd.DrawIndirect(computed.Buffer, 0, 2, DrawStride);
            },
            pool: computed.Pool)[0];

        image.ExpectQuadrants([Red, null, Blue, null], "DrawIndirect of GPU-written records for quads 0 and 2");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The argument barrier is real on Vulkan, where NRI turns ARGUMENT_BUFFER / INDIRECT into
    /// INDIRECT_COMMAND_READ at DRAW_INDIRECT: with the Khronos synchronization validation switched
    /// on for this device (and its shader-access heuristic, which sees the compute shader's
    /// descriptor write), the dispatch and the draw recorded with no barrier between, never
    /// submitted, draw a SYNC-HAZARD; recorded with the barrier, submitted, they draw none, and the
    /// image is right. The layer reads its settings from the process environment at instance
    /// creation, which .NET on Unix does not update, so without the control's hazard the test skips.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_argument_barrier_orders_the_compute_write_before_the_indirect_read_on_Vulkan(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipWhen(backend.Api != GraphicsAPI.VK, $"{backend}: no synchronization validation for buffers (the debug layer reported nothing for the missing barrier, measured)");
        string[] settings = ["VK_KHRONOS_VALIDATION_VALIDATE_SYNC", "VK_KHRONOS_VALIDATION_SYNCVAL_SHADER_ACCESSES_HEURISTIC"];
        var messages = new List<NriMessage>();
        var previous = settings.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach (var s in settings) Environment.SetEnvironmentVariable(s, "1");
        NriDevice device;
        try
        {
            device = TestDevice.Create(backend.Api, messages);
        }
        finally
        {
            for (var i = 0; i < settings.Length; i++) Environment.SetEnvironmentVariable(settings[i], previous[i]);
        }
        using var _ = device;
        RequireMultiDraw(device);

        using var scene = new TriangleScene(device);
        using var computed = new ComputedArguments(device);
        using var canvas = new Canvas(device);
        void DrawRecords(NriCommandBuffer cmd)
        {
            scene.Bind(cmd);
            cmd.DrawIndirect(computed.Buffer, 0, 2, DrawStride);
        }

        canvas.Render(before: cmd => computed.Write(cmd, barrier: false), inside: DrawRecords, pool: computed.Pool, submit: false);
        List<NriMessage> hazards;
        lock (messages) hazards = messages.Where(m => m.Text.Contains("SYNC-HAZARD", StringComparison.Ordinal)).ToList();
        foreach (var m in hazards) Record("control, no barrier: " + m.Text);
        Assert.SkipWhen(hazards.Count == 0, $"the Khronos synchronization validation did not activate ({settings[0]} is read from the process environment at instance creation; no SYNC-HAZARD for a dispatch and an indirect draw with no barrier between)");
        Assert.Contains(hazards, m => m.Text.Contains("vkCmdDrawIndirect", StringComparison.Ordinal));
        lock (messages) messages.Clear();

        var image = canvas.Render(before: cmd => computed.Write(cmd, barrier: true), inside: DrawRecords, pool: computed.Pool)[0];

        image.ExpectQuadrants([Red, null, Blue, null], "DrawIndirect of GPU-written records, synchronization validation on");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// DispatchIndirect at offset 12, past a decoy DispatchDesc {1, 1, 1}, of DispatchDesc {3, 2, 1}:
    /// Shaders/DrawGroups.hlsl's four threads per group each add 1 to their group's slot, so the six
    /// slots of groups (0..2, 0..1, 0) hold 4 and the other 58 hold 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void DispatchIndirect_runs_the_work_groups_of_the_record_at_the_offset(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.Equal(12, Unsafe.SizeOf<DispatchDesc>());

        const int slots = 64;
        using var arguments = Upload(device, new[]
        {
            new DispatchDesc { workGroupNumX = 1, workGroupNumY = 1, workGroupNumZ = 1 },
            new DispatchDesc { workGroupNumX = 3, workGroupNumY = 2, workGroupNumZ = 1 },
        }, BufferUsageBits.ARGUMENT);
        using var groups = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = slots * sizeof(uint), structureStride = sizeof(uint), usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var view = groups.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slots * sizeof(uint) });
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets =
            [
                new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER }]),
            ],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "DrawGroups.cs"), "main"),
        });
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 1 });
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.Update(0, 0, view);

        Run(device, cmd =>
        {
            cmd.Barrier(new NriBufferBarrier(groups, Nothing, CopyDestination));
            cmd.ZeroBuffer(groups, 0, NriApi.WHOLE_SIZE);
            cmd.Barrier(new NriBufferBarrier(groups, CopyDestination, ComputeStorage));
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.DispatchIndirect(arguments, (ulong)Unsafe.SizeOf<DispatchDesc>());
            cmd.Barrier(new NriBufferBarrier(groups, ComputeStorage, CopySource));
            cmd.CopyBuffer(readback, 0, groups, 0, slots * sizeof(uint));
        }, pool);

        var counts = MemoryMarshal.Cast<byte, uint>(readback.Map()).ToArray();
        readback.Unmap();

        var expected = new uint[slots];
        for (var y = 0; y < 2; y++)
        for (var x = 0; x < 3; x++)
            expected[y * 4 + x] = 4;
        Assert.Equal(expected, counts);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Draw parameters reach the vertex shader through NRI's documented path
    /// (Shaders/DrawParameters.hlsl): Vulkan's BaseVertex and BaseInstance built-ins, and on D3D12
    /// NRI's emulation, root constants NRI sets per draw when the layout has
    /// ENABLE_DRAW_PARAMETERS_EMULATION, and for indirect draws reads from the first two words of
    /// DrawBaseDesc / DrawIndexedBaseDesc records (the "modified draw command signatures" of
    /// NRIDescs.h). The flag is documented for D3D12 and Metal ("D3D12, METAL"), so portable code
    /// sets it on every backend: Vulkan renders once with it and once without, the same picture,
    /// from the plain DrawDesc / DrawIndexedDesc records the Vulkan backend reads and NRI.hlsl
    /// writes for SPIR-V. (NRI validation applied the D3D12 record layout on Vulkan too, refusing
    /// stride 16 with "'stride' is too small, expected >= 24"; patches/0043.) The colour encodes baseVertex (red)
    /// and baseInstance (green): quadrants 0 and 1 from direct draws with baseInstance 5 and 9 (3
    /// and 7 indexed), quadrants 2 and 3 from two indirect records with baseInstance 0 (a non-zero
    /// one in a Vulkan indirect draw needs drawIndirectFirstInstance, which NRI does not report), for
    /// Draw / DrawIndirect over the triangle lists and DrawIndexed / DrawIndexedIndirect over the
    /// indexed quads.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Draw_parameters_reach_the_vertex_shader_from_direct_and_indirect_draws(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultiDraw(device);
        Assert.SkipWhen(device.Desc.shaderFeatures.drawParameters == 0, $"{backend}: shaderFeatures.drawParameters = 0");
        Assert.Equal(24, Unsafe.SizeOf<DrawBaseDesc>());
        Assert.Equal(28, Unsafe.SizeOf<DrawIndexedBaseDesc>());
        var emulated = backend.Api == GraphicsAPI.D3D12;
        // The flag is set on every backend; on Vulkan once more without it, the same picture.
        foreach (var flag in emulated ? new[] { true } : new[] { true, false })
            DrawParameters(device, messages, emulated, flag);
    }

    private static void DrawParameters(NriDevice device, List<NriMessage> messages, bool emulated, bool flag)
    {
        var start = messages.Count;
        using var triangles = Upload(device, QuadrantTriangles(Quadrants), BufferUsageBits.VERTEX);
        using var quads = Upload(device, QuadrantQuads(Quadrants), BufferUsageBits.VERTEX);
        using var indices = Upload(device, QuadIndices(0), BufferUsageBits.INDEX);
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER,
            Flags = flag ? PipelineLayoutBits.ENABLE_DRAW_PARAMETERS_EMULATION : PipelineLayoutBits.NONE,
        });
        using var pipeline = CreateColorPipeline(device, layout, "DrawParameters", new NriVertexInputDesc
        {
            Attributes = [new NriVertexAttributeDesc("POSITION", 0, Location: 0, Offset: 0, Format.RG32_SFLOAT, StreamIndex: 0)],
            Streams = [new VertexStreamDesc { bindingSlot = 0, stepRate = VertexStreamStepRate.PER_VERTEX, stride = (ushort)ColorVertex.Stride }],
        });

        using var drawArguments = emulated
            ? Upload(device, new[] { DrawBase(12), DrawBase(18) }, BufferUsageBits.ARGUMENT)
            : Upload(device, new[] { Draw(2), Draw(3) }, BufferUsageBits.ARGUMENT);
        using var indexedArguments = emulated
            ? Upload(device, new[] { DrawIndexedBase(8), DrawIndexedBase(12) }, BufferUsageBits.ARGUMENT)
            : Upload(device, new[] { DrawIndexed(8), DrawIndexed(12) }, BufferUsageBits.ARGUMENT);
        var drawStride = emulated ? (uint)Unsafe.SizeOf<DrawBaseDesc>() : DrawStride;
        var indexedStride = emulated ? (uint)Unsafe.SizeOf<DrawIndexedBaseDesc>() : (uint)Unsafe.SizeOf<DrawIndexedDesc>();

        using var canvas = new Canvas(device);
        var nonIndexed = canvas.Render(cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetVertexBuffer(0, triangles, 0, ColorVertex.Stride);
            cmd.Draw(6, baseVertex: 0, baseInstance: 5);
            cmd.Draw(6, baseVertex: 6, baseInstance: 9);
            cmd.DrawIndirect(drawArguments, 0, 2, drawStride);
        })[0];
        var indexed = canvas.Render(cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetVertexBuffer(0, quads, 0, ColorVertex.Stride);
            cmd.SetIndexBuffer(indices, 0, IndexType.UINT32);
            cmd.DrawIndexed(6, baseVertex: 0, baseInstance: 3);
            cmd.DrawIndexed(6, baseVertex: 4, baseInstance: 7);
            cmd.DrawIndexedIndirect(indexedArguments, 0, 2, indexedStride);
        })[0];

        static Rgba Parameters(int baseVertex, int baseInstance) => new((byte)baseVertex, (byte)baseInstance, 255);
        var flagText = flag ? "flag set" : "flag not set";
        Assert.DoesNotContain(messages.Skip(start), m => m.IsError);
        nonIndexed.ExpectQuadrants([Parameters(0, 5), Parameters(6, 9), Parameters(12, 0), Parameters(18, 0)], $"Draw and DrawIndirect ({(emulated ? "DrawBaseDesc, emulated" : "DrawDesc, built-ins")}, {flagText})");
        indexed.ExpectQuadrants([Parameters(0, 3), Parameters(4, 7), Parameters(8, 0), Parameters(12, 0)], $"DrawIndexed and DrawIndexedIndirect ({(emulated ? "DrawIndexedBaseDesc, emulated" : "DrawIndexedDesc, built-ins")}, {flagText})");

        static DrawBaseDesc DrawBase(uint baseVertex) => new()
        {
            shaderEmulatedBaseVertex = baseVertex, shaderEmulatedBaseInstance = 0,
            vertexNum = 6, instanceNum = 1, baseVertex = baseVertex, baseInstance = 0,
        };
        static DrawIndexedBaseDesc DrawIndexedBase(int baseVertex) => new()
        {
            shaderEmulatedBaseVertex = baseVertex, shaderEmulatedBaseInstance = 0,
            indexNum = 6, instanceNum = 1, baseIndex = 0, baseVertex = baseVertex, baseInstance = 0,
        };
        static DrawIndexedDesc DrawIndexed(int baseVertex) => new() { indexNum = 6, instanceNum = 1, baseIndex = 0, baseVertex = baseVertex, baseInstance = 0 };
    }

    // ---------------------------------------------------------------- scenes and records

    /// <summary>Vulkan draws more than one record per call only with multiDrawIndirect; NRI reports it as drawIndirectMaxNum.</summary>
    private static void RequireMultiDraw(NriDevice device) =>
        Assert.SkipWhen(device.Desc.other.drawIndirectMaxNum < 2, $"{device.GraphicsApi}: other.drawIndirectMaxNum = {device.Desc.other.drawIndirectMaxNum}, no multi-draw indirect");

    /// <summary>The DrawDesc of quad <paramref name="quad"/> of <see cref="TriangleScene"/>.</summary>
    private static DrawDesc Draw(uint quad) => new() { vertexNum = 6, instanceNum = 1, baseVertex = quad * 6, baseInstance = 0 };

    /// <summary>{baseIndex 0, baseVertex +8} for quad 3 and {baseIndex 6, baseVertex -4} for quad 1, over <see cref="IndexedScene"/>.</summary>
    private static readonly PaddedDrawIndexed[] IndexedRecords =
    [
        new(new DrawIndexedDesc { indexNum = 6, instanceNum = 1, baseIndex = 0, baseVertex = 8, baseInstance = 0 }),
        new(new DrawIndexedDesc { indexNum = 6, instanceNum = 1, baseIndex = 6, baseVertex = -4, baseInstance = 0 }),
    ];

    /// <summary>A DrawIndexedDesc followed by a zero word: a 24-byte stride.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PaddedDrawIndexed(DrawIndexedDesc desc)
    {
        public const uint Stride = 24;
        public readonly DrawIndexedDesc Desc = desc;
        public readonly uint Padding = 0;
    }

    /// <summary>The 32 bytes of root constants Shaders/DrawArgs.hlsl copies out: two DrawDesc records.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TwoDraws
    {
        public DrawDesc First;
        public DrawDesc Second;
    }

    /// <summary>
    /// A device buffer of two DrawDesc records that Shaders/DrawArgs.hlsl writes, for quads 0 and 2,
    /// with its compute pipeline, layout (set 0: the buffer's storage view; root constants in space
    /// 1) and descriptor pool.
    /// </summary>
    private sealed class ComputedArguments : IDisposable
    {
        private readonly NriDescriptor _view;
        private readonly NriPipelineLayout _layout;
        private readonly NriPipeline _pipeline;
        private readonly NriDescriptorSet _set;

        public ComputedArguments(NriDevice device)
        {
            Buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc
            {
                size = 2 * DrawStride, structureStride = sizeof(uint), usage = BufferUsageBits.ARGUMENT | BufferUsageBits.SHADER_RESOURCE_STORAGE,
            });
            _view = Buffer.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
            _layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
            {
                RootRegisterSpace = 1,
                RootConstants = [new RootConstantDesc { registerIndex = 0, size = 2 * DrawStride, shaderStages = StageBits.COMPUTE_SHADER }],
                DescriptorSets =
                [
                    new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER }]),
                ],
                ShaderStages = StageBits.COMPUTE_SHADER,
            });
            _pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
            {
                PipelineLayout = _layout,
                Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "DrawArgs.cs"), "main"),
            });
            Pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 1 });
            _set = Pool.AllocateDescriptorSet(_layout, 0);
            _set.Update(0, 0, _view);
        }

        public NriBuffer Buffer { get; }
        public NriDescriptorPool Pool { get; }

        /// <summary>Zeroes the buffer, dispatches the write and, with <paramref name="barrier"/>, makes the records indirect arguments.</summary>
        public void Write(NriCommandBuffer cmd, bool barrier)
        {
            cmd.Barrier(new NriBufferBarrier(Buffer, Nothing, CopyDestination));
            cmd.ZeroBuffer(Buffer, 0, NriApi.WHOLE_SIZE);
            cmd.Barrier(new NriBufferBarrier(Buffer, CopyDestination, ComputeStorage));
            cmd.SetPipelineLayout(BindPoint.COMPUTE, _layout);
            cmd.SetPipeline(_pipeline);
            cmd.SetDescriptorSet(0, _set, BindPoint.COMPUTE);
            cmd.SetRootConstants(0, new TwoDraws { First = Draw(0), Second = Draw(2) }, bindPoint: BindPoint.COMPUTE);
            cmd.Dispatch(1);
            if (barrier)
                cmd.Barrier(new NriBufferBarrier(Buffer, ComputeStorage, new AccessStage { access = AccessBits.ARGUMENT_BUFFER, stages = StageBits.INDIRECT }));
        }

        public void Dispose()
        {
            Pool.Dispose();
            _pipeline.Dispose();
            _layout.Dispose();
            _view.Dispose();
            Buffer.Dispose();
        }
    }

    /// <summary>The four quadrant quads as one triangle list, six vertices each, drawn with Shaders/DrawColor.hlsl.</summary>
    private sealed class TriangleScene : IDisposable
    {
        private readonly NriBuffer _vertices;
        private readonly NriPipelineLayout _layout;
        private readonly NriPipeline _pipeline;

        public TriangleScene(NriDevice device)
        {
            _vertices = Upload(device, QuadrantTriangles(Quadrants), BufferUsageBits.VERTEX);
            _layout = CreateLayout(device);
            _pipeline = CreateColorPipeline(device, _layout, "DrawColor", InterleavedInput);
        }

        public void Bind(NriCommandBuffer cmd)
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, _layout);
            cmd.SetPipeline(_pipeline);
            cmd.SetVertexBuffer(0, _vertices, 0, ColorVertex.Stride);
        }

        public void Dispose()
        {
            _pipeline.Dispose();
            _layout.Dispose();
            _vertices.Dispose();
        }
    }

    /// <summary>The four quadrant quads, four vertices each, and a 32-bit index buffer of the 4-7 and 8-11 blocks.</summary>
    private sealed class IndexedScene : IDisposable
    {
        private readonly NriBuffer _vertices;
        private readonly NriBuffer _indices;
        private readonly NriPipelineLayout _layout;
        private readonly NriPipeline _pipeline;

        public IndexedScene(NriDevice device)
        {
            _vertices = Upload(device, QuadrantQuads(Quadrants), BufferUsageBits.VERTEX);
            _indices = Upload(device, [.. QuadIndices(4), .. QuadIndices(8)], BufferUsageBits.INDEX);
            _layout = CreateLayout(device);
            _pipeline = CreateColorPipeline(device, _layout, "DrawColor", InterleavedInput);
        }

        public void Bind(NriCommandBuffer cmd)
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, _layout);
            cmd.SetPipeline(_pipeline);
            cmd.SetVertexBuffer(0, _vertices, 0, ColorVertex.Stride);
            cmd.SetIndexBuffer(_indices, 0, IndexType.UINT32);
        }

        public void Dispose()
        {
            _pipeline.Dispose();
            _layout.Dispose();
            _indices.Dispose();
            _vertices.Dispose();
        }
    }
}
