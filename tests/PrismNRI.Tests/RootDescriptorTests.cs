// Root descriptors: buffer views bound straight into the pipeline layout by CmdSetRootDescriptor,
// with no descriptor set (push descriptors on Vulkan, root CBV/SRV/UAV parameters on D3D12). Each
// case reads back what the shader saw: the constant words, the structured-buffer words, the pixel
// colour. The offset cases hold the rule in CLAUDE.md (Memory and resources: the root descriptor
// offset): a view the size of a window slides by the offset on both backends, and NRI validation
// caps the offset at buffer size minus view offset minus view size.
//
// Register assignment as in DescriptorTests: all-zero VKBindingOffsets, so on Vulkan the binding of
// a root descriptor is its register index in the set of the root register space.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class RootDescriptorTests
{
    private const int Words = 16;             // [numthreads(16, 1, 1)] in RootDescriptors.hlsl and RootMixed.hlsl
    private const int OutputWords = 2 * Words;
    private const uint Window = 256;          // a constant-buffer placement on D3D12, a valid buffer offset on every Vulkan device

    /// <summary>RootDescriptors.hlsl: b0, t1, u2 in root register space 0, compute only.</summary>
    private static NriPipelineLayoutDesc AllRootLayout() => new()
    {
        RootDescriptors =
        [
            new RootDescriptorDesc { registerIndex = 0, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
            new RootDescriptorDesc { registerIndex = 1, descriptorType = DescriptorType.STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
            new RootDescriptorDesc { registerIndex = 2, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
        ],
        ShaderStages = StageBits.COMPUTE_SHADER,
    };

    private static void RequireRootDescriptors(NriDevice device, uint count) =>
        Assert.SkipWhen(device.Desc.pipelineLayout.rootDescriptorMaxNum < count,
            $"pipelineLayout.rootDescriptorMaxNum = {device.Desc.pipelineLayout.rootDescriptorMaxNum}; the layout has {count} root descriptors");

    /// <summary>A host-visible buffer holding <paramref name="words"/> from byte 0, zero after them.</summary>
    private static NriBuffer HostBuffer(NriDevice device, ReadOnlySpan<uint> words, ulong size, BufferUsageBits usage, uint structureStride = 0)
    {
        var buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = size, structureStride = structureStride, usage = usage });
        var mapped = buffer.Map();
        mapped.Clear();
        MemoryMarshal.AsBytes(words).CopyTo(mapped);
        buffer.Unmap();
        return buffer;
    }

    /// <summary><paramref name="count"/> words, distinct per <paramref name="tag"/>: 0xTTTT00ii.</summary>
    private static uint[] Pattern(uint tag, int count = Words) => [.. Enumerable.Range(0, count).Select(i => (tag << 16) | (uint)i)];

    /// <summary>A buffer of <paramref name="windows"/> windows of <see cref="Window"/> bytes, window k starting with <c>Pattern(tag + k)</c>.</summary>
    private static uint[] Windows(uint tag, int windows)
    {
        var words = new uint[windows * Window / sizeof(uint)];
        for (var k = 0; k < windows; k++) Pattern(tag + (uint)k).CopyTo(words, k * (int)Window / sizeof(uint));
        return words;
    }

    private static uint[] ReadWords(NriBuffer readback, int count)
    {
        var words = MemoryMarshal.Cast<byte, uint>(readback.Map())[..count].ToArray();
        readback.Unmap();
        return words;
    }

    private static string Hex(IEnumerable<uint> words) => string.Join(" ", words.Select(w => w.ToString("X8")));

    /// <summary>The output buffer the compute shaders write and the readback it is copied to.</summary>
    private sealed class Output : IDisposable
    {
        public readonly NriBuffer Buffer;
        public readonly NriDescriptor View;
        public readonly NriBuffer Readback;

        public Output(NriDevice device)
        {
            Buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = OutputWords * sizeof(uint), structureStride = sizeof(uint), usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
            View = Buffer.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
            Readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = OutputWords * sizeof(uint) });
        }

        public void BeforeDispatch(NriCommandBuffer cmd) => cmd.Barrier(new NriBufferBarrier(Buffer, Nothing, ComputeStorage));

        public void CopyOut(NriCommandBuffer cmd)
        {
            cmd.Barrier(new NriBufferBarrier(Buffer, ComputeStorage, CopySource));
            cmd.CopyBuffer(Readback, 0, Buffer, 0, OutputWords * sizeof(uint));
        }

        public uint[] Read() => ReadWords(Readback, OutputWords);

        public void Dispose()
        {
            Readback.Dispose();
            View.Dispose();
            Buffer.Dispose();
        }
    }

    /// <summary>
    /// A root constant buffer, a root structured buffer and a root storage buffer on the compute bind
    /// point, no descriptor set: Output[0..15] are the 16 constant words, Output[16..31] the first
    /// 16 words of the structured buffer.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Root_constant_structured_and_storage_buffers_feed_a_compute_dispatch(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireRootDescriptors(device, 3);

        var constants = Pattern(0xCB00);
        var source = Pattern(0x5B00);
        using var layout = device.CreatePipelineLayout(AllRootLayout());
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "RootDescriptors.cs"), "main"),
        });
        using var constantBuffer = HostBuffer(device, constants, Window, BufferUsageBits.CONSTANT);
        using var constantView = constantBuffer.CreateView(BufferView.CONSTANT_BUFFER, 0, Window);
        using var sourceBuffer = HostBuffer(device, source, Words * sizeof(uint), BufferUsageBits.SHADER_RESOURCE, sizeof(uint));
        using var sourceView = sourceBuffer.CreateView(BufferView.STRUCTURED_BUFFER, structureStride: sizeof(uint));
        using var output = new Output(device);

        Run(device, cmd =>
        {
            output.BeforeDispatch(cmd);
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetRootDescriptor(0, constantView, 0, BindPoint.COMPUTE);
            cmd.SetRootDescriptor(1, sourceView, 0, BindPoint.COMPUTE);
            cmd.SetRootDescriptor(2, output.View, 0, BindPoint.COMPUTE);
            cmd.Dispatch(1);
            output.CopyOut(cmd);
        });

        var words = output.Read();
        Assert.True(constants.AsSpan().SequenceEqual(words.AsSpan(0, Words)), $"from the root constant buffer: {Hex(words[..Words])}, expected {Hex(constants)}");
        Assert.True(source.AsSpan().SequenceEqual(words.AsSpan(Words, Words)), $"from the root structured buffer: {Hex(words[Words..])}, expected {Hex(source)}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The window rule: views of 256 bytes over buffers of two 256-byte windows, bound at root offset
    /// 0 and then 256, read the first and then the second window, on both backends (Vulkan slides the
    /// view's range, D3D12 adds the offset to the address). The two dispatches differ only in the
    /// offsets and in which output the root storage buffer names, so this is also root descriptors
    /// switched between two dispatches with nothing else rebound.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_view_the_size_of_a_window_slides_by_the_root_offset(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireRootDescriptors(device, 3);
        Assert.SkipWhen(device.Desc.features.nonConstantBufferRootDescriptorOffset == 0,
            "features.nonConstantBufferRootDescriptorOffset = 0: the structured buffer cannot take a root offset");

        var constants = Windows(0xCB00, 2);
        var source = Windows(0x5B00, 2);
        using var layout = device.CreatePipelineLayout(AllRootLayout());
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "RootDescriptors.cs"), "main"),
        });
        using var constantBuffer = HostBuffer(device, constants, 2 * Window, BufferUsageBits.CONSTANT);
        using var constantView = constantBuffer.CreateView(BufferView.CONSTANT_BUFFER, 0, Window);
        using var sourceBuffer = HostBuffer(device, source, 2 * Window, BufferUsageBits.SHADER_RESOURCE, sizeof(uint));
        using var sourceView = sourceBuffer.CreateView(BufferView.STRUCTURED_BUFFER, 0, Window, structureStride: sizeof(uint));
        using var first = new Output(device);
        using var second = new Output(device);

        Run(device, cmd =>
        {
            first.BeforeDispatch(cmd);
            second.BeforeDispatch(cmd);
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetRootDescriptor(0, constantView, 0);
            cmd.SetRootDescriptor(1, sourceView, 0);
            cmd.SetRootDescriptor(2, first.View);
            cmd.Dispatch(1);
            cmd.SetRootDescriptor(0, constantView, Window);
            cmd.SetRootDescriptor(1, sourceView, Window);
            cmd.SetRootDescriptor(2, second.View);
            cmd.Dispatch(1);
            first.CopyOut(cmd);
            second.CopyOut(cmd);
        });

        var expected0 = Pattern(0xCB00).Concat(Pattern(0x5B00)).ToArray();
        var expected1 = Pattern(0xCB01).Concat(Pattern(0x5B01)).ToArray();
        var words0 = first.Read();
        var words1 = second.Read();
        Record($"{backend}: offset 0 read {Hex(words0[..2])} .. {Hex(words0[Words..(Words + 2)])}; offset {Window} read {Hex(words1[..2])} .. {Hex(words1[Words..(Words + 2)])}");
        Assert.True(expected0.AsSpan().SequenceEqual(words0), $"root offset 0: {Hex(words0)}, expected {Hex(expected0)}");
        Assert.True(expected1.AsSpan().SequenceEqual(words1), $"root offset {Window}: {Hex(words1)}, expected {Hex(expected1)}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NRI validation caps a root offset at buffer size minus view offset minus view size
    /// (Validation/DescriptorVal.hpp, the size resolved in DeviceVal::CreateDescriptor): over a
    /// 512-byte buffer, a whole-buffer view takes offset 0 only, and a 256-byte view up to 256. The
    /// refused calls never reach the backend (NRI_RETURN_ON_FAILURE); nothing is dispatched or submitted.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void NRI_validation_refuses_a_root_offset_past_the_end_of_the_view(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireRootDescriptors(device, 3);

        using var layout = device.CreatePipelineLayout(AllRootLayout());
        using var buffer = HostBuffer(device, [], 2 * Window, BufferUsageBits.CONSTANT);
        using var whole = buffer.CreateView(BufferView.CONSTANT_BUFFER);
        using var window = buffer.CreateView(BufferView.CONSTANT_BUFFER, 0, Window);

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var cmd = allocator.CreateCommandBuffer();
        cmd.Begin();
        cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);

        List<NriMessage> Provoke(Action call)
        {
            int start;
            lock (messages) start = messages.Count;
            call();
            lock (messages) return messages.Skip(start).ToList();
        }

        var wholeAtWindow = Provoke(() => cmd.SetRootDescriptor(0, whole, Window));
        var windowAtWindow = Provoke(() => cmd.SetRootDescriptor(0, window, Window));
        var windowPastEnd = Provoke(() => cmd.SetRootDescriptor(0, window, 2 * Window));
        cmd.End();
        foreach (var m in wholeAtWindow.Concat(windowAtWindow).Concat(windowPastEnd)) Record("message: " + m);

        var refused = Assert.Single(wholeAtWindow, m => m.IsError);
        Assert.Contains($"'offset={Window}' must be <= maximum root descriptor 'offset=0'", refused.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(windowAtWindow, m => m.IsError);
        refused = Assert.Single(windowPastEnd, m => m.IsError);
        Assert.Contains($"'offset={2 * Window}' must be <= maximum root descriptor 'offset={Window}'", refused.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A layout with a descriptor set (the output, set 0) and root descriptors in root register space 1
    /// (RootMixed.hlsl): the set is bound once; between the two dispatches only the root constant
    /// buffer (where to write) and the root structured buffer (what to copy) change. The first
    /// dispatch's words land at 0, the second's at 16, so each dispatch saw its own root descriptors
    /// and the set stayed bound across the switch. On Vulkan the root descriptors are pushed into set 1
    /// of the same layout, which leaves set 0 bound.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Root_descriptors_switch_between_dispatches_while_the_descriptor_set_stays_bound(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireRootDescriptors(device, 2);

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            RootRegisterSpace = 1,
            RootDescriptors =
            [
                new RootDescriptorDesc { registerIndex = 0, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
                new RootDescriptorDesc { registerIndex = 1, descriptorType = DescriptorType.STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
            ],
            DescriptorSets =
            [
                new NriDescriptorSetDesc(0,
                [
                    new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
                ]),
            ],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "RootMixed.cs"), "main"),
        });
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 1 });
        using var output = new Output(device);
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.Update(0, 0, output.View);

        // Where each dispatch writes: the first word of each 256-byte window, each window its own view.
        var placements = new uint[2 * Window / sizeof(uint)];
        placements[Window / sizeof(uint)] = Words;
        using var placementBuffer = HostBuffer(device, placements, 2 * Window, BufferUsageBits.CONSTANT);
        using var atZero = placementBuffer.CreateView(BufferView.CONSTANT_BUFFER, 0, Window);
        using var atWords = placementBuffer.CreateView(BufferView.CONSTANT_BUFFER, Window, Window);
        var a = Pattern(0xA000);
        var b = Pattern(0xB000);
        using var sourceA = HostBuffer(device, a, Words * sizeof(uint), BufferUsageBits.SHADER_RESOURCE, sizeof(uint));
        using var sourceB = HostBuffer(device, b, Words * sizeof(uint), BufferUsageBits.SHADER_RESOURCE, sizeof(uint));
        using var viewA = sourceA.CreateView(BufferView.STRUCTURED_BUFFER, structureStride: sizeof(uint));
        using var viewB = sourceB.CreateView(BufferView.STRUCTURED_BUFFER, structureStride: sizeof(uint));

        Run(device, cmd =>
        {
            output.BeforeDispatch(cmd);
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetDescriptorSet(0, set);
            cmd.SetRootDescriptor(0, atZero);
            cmd.SetRootDescriptor(1, viewA);
            cmd.Dispatch(1);
            // Disjoint words of the same buffer; the barrier keeps synchronization validation's
            // whole-descriptor view of the two writes ordered.
            cmd.Barrier(new NriBufferBarrier(output.Buffer, ComputeStorage, ComputeStorage));
            cmd.SetRootDescriptor(0, atWords);
            cmd.SetRootDescriptor(1, viewB);
            cmd.Dispatch(1);
            output.CopyOut(cmd);
        }, pool);

        var words = output.Read();
        Assert.True(a.AsSpan().SequenceEqual(words.AsSpan(0, Words)), $"first dispatch: {Hex(words[..Words])}, expected {Hex(a)}");
        Assert.True(b.AsSpan().SequenceEqual(words.AsSpan(Words, Words)), $"second dispatch: {Hex(words[Words..])}, expected {Hex(b)}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The graphics bind point: a root constant buffer supplies the pixel colour (RootColor.hlsl).
    /// Two draws of one full-viewport triangle, the left half scissored for the first with a red
    /// constant buffer and the right half for the second with a blue one; the root descriptor is set
    /// with the bind point inherited from SetPipelineLayout(GRAPHICS).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_root_constant_buffer_colours_the_pixels_of_a_draw(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireRootDescriptors(device, 1);
        const ushort size = 16;

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            RootDescriptors = [new RootDescriptorDesc { registerIndex = 0, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.FRAGMENT_SHADER }],
            ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER,
        });
        using var pipeline = device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            InputAssembly = new InputAssemblyDesc { topology = Topology.TRIANGLE_LIST },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE },
            OutputMerger = new NriOutputMergerDesc { Colors = [new ColorAttachmentDesc { format = Format.RGBA8_UNORM, colorWriteMask = ColorWriteBits.RGBA }] },
            Shaders =
            [
                new NriShaderDesc(StageBits.VERTEX_SHADER, PipelineTests.Bytecode(device, "RootColor.vs"), "vsMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, PipelineTests.Bytecode(device, "RootColor.ps"), "psMain"),
            ],
        });
        using var red = HostBuffer(device, MemoryMarshal.Cast<float, uint>(new float[] { 1, 0, 0, 1 }.AsSpan()), Window, BufferUsageBits.CONSTANT);
        using var blue = HostBuffer(device, MemoryMarshal.Cast<float, uint>(new float[] { 0, 0, 1, 1 }.AsSpan()), Window, BufferUsageBits.CONSTANT);
        using var redView = red.CreateView(BufferView.CONSTANT_BUFFER, 0, Window);
        using var blueView = blue.CreateView(BufferView.CONSTANT_BUFFER, 0, Window);
        using var target = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM,
            width = size, height = size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var targetView = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        var rowPitch = (uint)Align(size * 4u, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)Align((ulong)rowPitch * size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        Run(device, cmd =>
        {
            cmd.Barrier(new NriTextureBarrier(target, Undefined, ColorAttachment));
            var clear = new ClearValue();
            clear.color.f = new Color32f { x = 0, y = 1, z = 0, w = 1 };
            cmd.BeginRendering([new NriAttachment(targetView, clear, LoadOp.CLEAR, StoreOp.STORE)]);
            cmd.SetViewport(new Viewport { width = size, height = size, depthMax = 1 });
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetScissor(new Rect { x = 0, y = 0, width = size / 2, height = size });
            cmd.SetRootDescriptor(0, redView);
            cmd.Draw(3);
            cmd.SetScissor(new Rect { x = size / 2, y = 0, width = size / 2, height = size });
            cmd.SetRootDescriptor(0, blueView);
            cmd.Draw(3);
            cmd.EndRendering();
            cmd.Barrier(new NriTextureBarrier(target, ColorAttachment, TextureCopySource));
            cmd.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = rowPitch, slicePitch = slicePitch }, target,
                new TextureRegionDesc { width = size, height = size, depth = 1, planes = PlaneBits.COLOR });
        });

        var pixels = readback.Map().ToArray();
        readback.Unmap();
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var i = (int)(y * rowPitch) + x * 4;
            var pixel = (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
            var expected = x < size / 2 ? ((byte)255, (byte)0, (byte)0, (byte)255) : ((byte)0, (byte)0, (byte)255, (byte)255);
            Assert.True(pixel == expected, $"pixel ({x},{y}) is {pixel}, expected {expected} ({(x < size / 2 ? "red" : "blue")} root constant buffer)");
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriDescriptorSet's object equality: a set equals itself boxed and finds itself in a hash set of
    /// objects; it equals no other set of the same pool, no null and no object of another type
    /// (its handle as a boxed nint included).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_descriptor_set_equals_itself_boxed_and_nothing_else(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER }])],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 2, storageStructuredBufferMaxNum = 2 });
        var sets = pool.AllocateDescriptorSets(layout, 0, 2);

        object boxed = sets[0];
        Assert.True(sets[0].Equals(boxed));
        Assert.True(boxed.Equals(sets[0]));
        Assert.Contains(sets[0], new HashSet<object> { boxed });
        Assert.False(sets[0].Equals((object)sets[1]));
        Assert.DoesNotContain(sets[1], new HashSet<object> { boxed });
        Assert.False(sets[0].Equals(null));
        Assert.False(sets[0].Equals((object)sets[0].NriHandle));
        Assert.False(sets[0].Equals("a descriptor set"));
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
