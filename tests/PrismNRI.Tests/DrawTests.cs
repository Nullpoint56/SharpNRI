// Vertex and index input and indexed draws: CmdSetVertexBuffers (one call for several streams and
// the single-buffer form), CmdSetIndexBuffer at an offset with both index types, both CmdDrawIndexed
// overloads with baseIndex, positive and negative baseVertex and baseInstance, per-instance streams,
// and primitive restart. Offscreen, read back and checked pixel by pixel, as TriangleTests does.
//
// Every colour is flat (Shaders/DrawColor.hlsl, Shaders/DrawInstanced.hlsl) and made of 0 and 255,
// so a covered pixel is exactly its triangle's colour on every backend, and every edge of a quad
// lies on a pixel boundary, so coverage is exact: the whole image is compared, pixel for pixel,
// with the one it must be. The only pixels left out are the ones on the diagonal two triangles of
// different colours share, which the rasterizer's tie-break rule gives to either.
//
// Canvas, Image, Rgba and the vertex helpers at the bottom are the render harness of DrawTests,
// IndirectTests and RenderStateTests.

using System.Runtime.InteropServices;
using System.Text;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class DrawTests
{
    // ---------------------------------------------------------------- tests

    /// <summary>
    /// One interleaved stream (float2 position, RGBA8_UNORM colour) of four vertices and a 16-bit
    /// index buffer of six indices: a quad of two triangles sharing an edge. The triangles are
    /// flat-coloured by their first vertex, 0 (red) and 3 (green); vertices 1 and 2 are blue and
    /// yellow, so an index buffer read in another order, or a provoking vertex other than the first,
    /// shows up as a colour that must not be there.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_interleaved_stream_and_a_16_bit_index_buffer_draw_two_flat_coloured_triangles(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var image = RenderSplitQuadInterleaved(device);
        image.Expect(SplitQuadExpectation, "interleaved stream, 16-bit indices");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The same quad from two streams, positions in slot 0 and colours in slot 1, bound with one
    /// SetVertexBuffers(0, [positions, colours]) call at offsets 64 and 32 into buffers whose leading
    /// bytes are 0xCD: the image is byte for byte the interleaved one.
    /// The streams are declared in slot order (Streams[i].bindingSlot == i): NRI's Vulkan backend
    /// takes an attribute's binding from its streamIndex (PipelineVK.hpp, vertexAttributeDesc.binding
    /// = attribute.streamIndex) where D3D12 takes Streams[streamIndex].bindingSlot, so streams
    /// declared out of slot order fetch the wrong buffers on Vulkan (measured: nothing drawn, and
    /// VUID-vkCmdBindVertexBuffers2-pStrides-06209 for the stride of the wrong binding).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Two_streams_at_offsets_bound_in_one_call_draw_the_interleaved_image(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var interleaved = RenderSplitQuadInterleaved(device);

        var quad = SplitQuad();
        using var positions = Upload(device, quad.Select(v => new Vector2(v.X, v.Y)).ToArray(), BufferUsageBits.VERTEX, leadingBytes: 64);
        using var colors = Upload(device, quad.Select(v => v.Color).ToArray(), BufferUsageBits.VERTEX, leadingBytes: 32);
        using var indices = Upload(device, SplitQuadIndices, BufferUsageBits.INDEX);
        using var layout = CreateLayout(device);
        using var pipeline = CreateColorPipeline(device, layout, "DrawColor", new NriVertexInputDesc
        {
            Attributes =
            [
                new NriVertexAttributeDesc("POSITION", 0, Location: 0, Offset: 0, Format.RG32_SFLOAT, StreamIndex: 0),
                new NriVertexAttributeDesc("COLOR", 0, Location: 1, Offset: 0, Format.RGBA8_UNORM, StreamIndex: 1),
            ],
            Streams =
            [
                new VertexStreamDesc { bindingSlot = 0, stepRate = VertexStreamStepRate.PER_VERTEX, stride = 8 },
                new VertexStreamDesc { bindingSlot = 1, stepRate = VertexStreamStepRate.PER_VERTEX, stride = 4 },
            ],
        });

        using var canvas = new Canvas(device);
        var twoStreams = canvas.Render(cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetVertexBuffers(0, [new NriVertexBuffer(positions, 64, 8), new NriVertexBuffer(colors, 32, 4)]);
            cmd.SetIndexBuffer(indices, 0, IndexType.UINT16);
            cmd.DrawIndexed(6);
        })[0];

        twoStreams.Expect(SplitQuadExpectation, "two streams at offsets");
        Assert.Equal(interleaved.Bytes, twoStreams.Bytes);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Four quads, one per quadrant, are vertices 0-3, 4-7, 8-11 and 12-15. A 32-bit index buffer
    /// holds three blocks of quad indices, for vertices 0-3, 4-7 and 8-11, and is bound 24 bytes in,
    /// past the first block. DrawIndexed(6, baseIndex 0, baseVertex +8) takes the 4-7 block and
    /// draws quad 3; DrawIndexed(6, baseIndex 6, baseVertex -4) takes the 8-11 block and draws quad
    /// 1. Every way of getting it wrong stays in range and draws another quad: an ignored offset
    /// draws quads 2 and 0, an ignored baseIndex quad 0, an ignored baseVertex quads 1 and 2.
    /// Recorded once through each DrawIndexed overload, with the same image.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_32_bit_index_buffer_at_an_offset_with_baseIndex_and_signed_baseVertex_selects_the_vertices(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var vertices = Upload(device, QuadrantQuads(Quadrants), BufferUsageBits.VERTEX);
        using var indices = Upload(device, [.. QuadIndices(0), .. QuadIndices(4), .. QuadIndices(8)], BufferUsageBits.INDEX);
        using var layout = CreateLayout(device);
        using var pipeline = CreateColorPipeline(device, layout, "DrawColor", InterleavedInput);
        using var canvas = new Canvas(device);

        void Bind(NriCommandBuffer cmd)
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetVertexBuffer(0, vertices, 0, ColorVertex.Stride);
            cmd.SetIndexBuffer(indices, 6 * sizeof(uint), IndexType.UINT32);
        }

        var byArguments = canvas.Render(cmd =>
        {
            Bind(cmd);
            cmd.DrawIndexed(6, baseIndex: 0, baseVertex: 8);
            cmd.DrawIndexed(6, baseIndex: 6, baseVertex: -4);
        })[0];
        var byDesc = canvas.Render(cmd =>
        {
            Bind(cmd);
            cmd.DrawIndexed(new DrawIndexedDesc { indexNum = 6, instanceNum = 1, baseIndex = 0, baseVertex = 8, baseInstance = 0 });
            cmd.DrawIndexed(new DrawIndexedDesc { indexNum = 6, instanceNum = 1, baseIndex = 6, baseVertex = -4, baseInstance = 0 });
        })[0];

        byArguments.ExpectQuadrants([null, Green, null, Yellow], "DrawIndexed(indexNum, ..., baseVertex), +8 and -4");
        byDesc.ExpectQuadrants([null, Green, null, Yellow], "DrawIndexed(in DrawIndexedDesc), +8 and -4");
        Assert.Equal(byArguments.Bytes, byDesc.Bytes);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Per-instance data: one quad's four vertices in stream 0 (per vertex), and in stream 1 (per
    /// instance) three records of a clip-space offset and a colour, which place the quad in
    /// quadrant 0 (red), 1 (green) and 3 (yellow). DrawIndexed(6, instanceNum 2, baseInstance 1)
    /// draws instance records 1 and 2: baseInstance applies to the per-instance fetch, so quadrant
    /// 0 stays at the clear colour.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_per_instance_stream_is_fetched_from_baseInstance(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // A 24x24 quad at the target's top-left corner; each instance moves it into a quadrant.
        Vector2[] corner = [new(ClipX(0), ClipY(0)), new(ClipX(24), ClipY(0)), new(ClipX(0), ClipY(24)), new(ClipX(24), ClipY(24))];
        InstanceData[] instances =
        [
            new(Quadrants[0].X0 / 32f, -Quadrants[0].Y0 / 32f, Red),
            new(Quadrants[1].X0 / 32f, -Quadrants[1].Y0 / 32f, Green),
            new(Quadrants[3].X0 / 32f, -Quadrants[3].Y0 / 32f, Yellow),
        ];
        using var vertices = Upload(device, corner, BufferUsageBits.VERTEX);
        using var perInstance = Upload(device, instances, BufferUsageBits.VERTEX);
        using var indices = Upload(device, QuadIndices(0).Select(i => (ushort)i).ToArray(), BufferUsageBits.INDEX);
        using var layout = CreateLayout(device);
        using var pipeline = CreateColorPipeline(device, layout, "DrawInstanced", new NriVertexInputDesc
        {
            Attributes =
            [
                new NriVertexAttributeDesc("POSITION", 0, Location: 0, Offset: 0, Format.RG32_SFLOAT, StreamIndex: 0),
                new NriVertexAttributeDesc("OFFSET", 0, Location: 1, Offset: 0, Format.RG32_SFLOAT, StreamIndex: 1),
                new NriVertexAttributeDesc("COLOR", 0, Location: 2, Offset: 8, Format.RGBA8_UNORM, StreamIndex: 1),
            ],
            Streams =
            [
                new VertexStreamDesc { bindingSlot = 0, stepRate = VertexStreamStepRate.PER_VERTEX, stride = 8 },
                new VertexStreamDesc { bindingSlot = 1, stepRate = VertexStreamStepRate.PER_INSTANCE, stride = InstanceData.Stride },
            ],
        });

        using var canvas = new Canvas(device);
        var image = canvas.Render(cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetVertexBuffers(0, [new NriVertexBuffer(vertices, 0, 8), new NriVertexBuffer(perInstance, 0, InstanceData.Stride)]);
            cmd.SetIndexBuffer(indices, 0, IndexType.UINT16);
            cmd.DrawIndexed(6, instanceNum: 2, baseInstance: 1);
        })[0];

        image.ExpectQuadrants([null, Green, null, Yellow], "instances 1 and 2 of three");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Primitive restart (InputAssemblyDesc.primitiveRestart = INDICES_UINT16): a triangle strip of
    /// indices 0-3, 0xFFFF, 4-7 draws two separate quads, in quadrants 0 and 3, and nothing between
    /// them. Without the restart the strip would go on through index 0xFFFF and join the quads with
    /// triangles across the centre of the target.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_strip_cut_by_the_restart_index_draws_two_separate_quads(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var vertices = Upload(device, [.. Quad(Quadrants[0], Red), .. Quad(Quadrants[3], Yellow)], BufferUsageBits.VERTEX);
        using var indices = Upload(device, new ushort[] { 0, 1, 2, 3, 0xFFFF, 4, 5, 6, 7 }, BufferUsageBits.INDEX);
        using var layout = CreateLayout(device);
        using var pipeline = CreateColorPipeline(device, layout, "DrawColor", InterleavedInput, Topology.TRIANGLE_STRIP, PrimitiveRestart.INDICES_UINT16);

        using var canvas = new Canvas(device);
        var image = canvas.Render(cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetVertexBuffer(0, vertices, 0, ColorVertex.Stride);
            cmd.SetIndexBuffer(indices, 0, IndexType.UINT16);
            cmd.DrawIndexed(9);
        })[0];

        image.ExpectQuadrants([Red, null, null, Yellow], "strip 0-3, restart, 4-7");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- the quad of two triangles

    /// <summary>
    /// The quad [8, 56) x [8, 56): vertices top-left (red), top-right (blue), bottom-left (yellow),
    /// bottom-right (green); triangles 0-1-2 and 3-2-1 share the edge from top-right to bottom-left.
    /// </summary>
    private static ColorVertex[] SplitQuad() =>
    [
        new(ClipX(8), ClipY(8), Red),
        new(ClipX(56), ClipY(8), Blue),
        new(ClipX(8), ClipY(56), Yellow),
        new(ClipX(56), ClipY(56), Green),
    ];

    private static readonly ushort[] SplitQuadIndices = [0, 1, 2, 3, 2, 1];

    /// <summary>Red above the shared edge (x + y = 64 through pixel centres), green below, black outside; the edge itself is either.</summary>
    private static Rgba? SplitQuadExpectation(int x, int y)
    {
        if (x < 8 || x >= 56 || y < 8 || y >= 56) return Black;
        var d = x + 0.5 + y + 0.5 - 64; // signed distance to the shared edge, times sqrt 2
        if (Math.Abs(d) <= 1.5) return null;
        return d < 0 ? Red : Green;
    }

    private static Image RenderSplitQuadInterleaved(NriDevice device)
    {
        using var vertices = Upload(device, SplitQuad(), BufferUsageBits.VERTEX);
        using var indices = Upload(device, SplitQuadIndices, BufferUsageBits.INDEX);
        using var layout = CreateLayout(device);
        using var pipeline = CreateColorPipeline(device, layout, "DrawColor", InterleavedInput);
        using var canvas = new Canvas(device);
        return canvas.Render(cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetVertexBuffer(0, vertices, 0, ColorVertex.Stride);
            cmd.SetIndexBuffer(indices, 0, IndexType.UINT16);
            cmd.DrawIndexed(6);
        })[0];
    }

    // ---------------------------------------------------------------- harness: colours and images

    internal readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
    {
        /// <summary>The four bytes as one RGBA8_UNORM word, R in the lowest byte.</summary>
        public uint Packed => (uint)(R | G << 8 | B << 16 | A << 24);

        public Rgba(Color32f c) : this(Unorm(c.x), Unorm(c.y), Unorm(c.z), Unorm(c.w)) { }

        public Color32f ToFloat() => new() { x = R / 255f, y = G / 255f, z = B / 255f, w = A / 255f };

        private static byte Unorm(float v) => (byte)Math.Round(Math.Clamp(v, 0f, 1f) * 255f);

        public override string ToString() => $"({R},{G},{B},{A})";
    }

    internal static readonly Rgba Black = new(0, 0, 0);
    internal static readonly Rgba Red = new(255, 0, 0);
    internal static readonly Rgba Green = new(0, 255, 0);
    internal static readonly Rgba Blue = new(0, 0, 255);
    internal static readonly Rgba Yellow = new(255, 255, 0);
    internal static readonly Rgba White = new(255, 255, 255);

    /// <summary>A pixel rectangle [X0, X1) x [Y0, Y1) of the target, y down.</summary>
    internal readonly record struct PixelRect(int X0, int Y0, int X1, int Y1)
    {
        public bool Contains(int x, int y) => x >= X0 && x < X1 && y >= Y0 && y < Y1;
    }

    /// <summary>One quad per quadrant, 4 pixels in from the quadrant's edges: 0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right.</summary>
    internal static readonly PixelRect[] Quadrants = [new(4, 4, 28, 28), new(36, 4, 60, 28), new(4, 36, 28, 60), new(36, 36, 60, 60)];

    /// <summary>The quadrants' colours in <see cref="QuadrantQuads"/>: red, green, blue, yellow.</summary>
    internal static readonly Rgba[] QuadrantColors = [Red, Green, Blue, Yellow];

    /// <summary>A 64x64 RGBA8 readback, tightly packed, row-major, y down.</summary>
    internal sealed class Image(byte[] bytes)
    {
        public byte[] Bytes => bytes;

        public Rgba this[int x, int y]
        {
            get
            {
                var i = (y * Canvas.Size + x) * 4;
                return new Rgba(bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3]);
            }
        }

        /// <summary>
        /// Every pixel is what <paramref name="expected"/> says, within <paramref name="tolerance"/>
        /// per channel; a null expectation leaves the pixel out. The failure names the first
        /// mismatches and draws the image.
        /// </summary>
        public void Expect(Func<int, int, Rgba?> expected, string what, int tolerance = 0)
        {
            var mismatches = new List<string>();
            var count = 0;
            for (var y = 0; y < Canvas.Size; y++)
            for (var x = 0; x < Canvas.Size; x++)
            {
                if (expected(x, y) is not { } e) continue;
                var p = this[x, y];
                if (Math.Abs(p.R - e.R) <= tolerance && Math.Abs(p.G - e.G) <= tolerance && Math.Abs(p.B - e.B) <= tolerance && Math.Abs(p.A - e.A) <= tolerance) continue;
                if (count++ < 4) mismatches.Add($"({x},{y}) is {p}, expected {e}");
            }
            Assert.True(count == 0, $"{what}: {count} pixels differ: {string.Join("; ", mismatches)}\n{Map()}");
        }

        /// <summary>The quad of quadrant q has colour <paramref name="colors"/>[q], or the clear colour where that is null; everything else is the clear colour.</summary>
        public void ExpectQuadrants(Rgba?[] colors, string what, int tolerance = 0) =>
            Expect((x, y) =>
            {
                for (var q = 0; q < Quadrants.Length; q++)
                    if (Quadrants[q].Contains(x, y)) return colors[q] ?? Black;
                return Black;
            }, what, tolerance);

        /// <summary>Every 4th pixel of every 4th row as one character: . black, R G B Y W the full colours, ? anything else.</summary>
        public string Map()
        {
            var map = new StringBuilder();
            for (var y = 1; y < Canvas.Size; y += 4)
            {
                for (var x = 1; x < Canvas.Size; x += 4)
                {
                    var p = this[x, y];
                    map.Append(p == Black ? '.' : p == Red ? 'R' : p == Green ? 'G' : p == Blue ? 'B' : p == Yellow ? 'Y' : p == White ? 'W' : '?');
                }
                map.Append('\n');
            }
            return map.ToString();
        }
    }

    // ---------------------------------------------------------------- harness: the render pass

    /// <summary>
    /// A 64x64 offscreen render: colour attachments (RGBA8_UNORM, cleared to black) and an optional
    /// depth-stencil attachment, cleared at BeginRendering; the caller's recording before the pass
    /// and inside it, with the viewport and scissor covering the target; the colour attachments
    /// read back. A depth-stencil format with stencil is passed as both the depth and the stencil
    /// attachment, so both planes are cleared on both backends.
    /// </summary>
    internal sealed class Canvas : IDisposable
    {
        public const int Size = 64;

        private readonly NriDevice _device;
        private readonly NriTexture[] _colors;
        private readonly NriBuffer[] _readbacks;
        private readonly NriTexture? _depthStencil;
        private readonly uint _rowPitch;
        private readonly uint _slicePitch;

        public Canvas(NriDevice device, int colorNum = 1, Format depthStencilFormat = Format.UNKNOWN)
        {
            _device = device;
            _colors = new NriTexture[colorNum];
            _readbacks = new NriBuffer[colorNum];
            ColorViews = new NriDescriptor[colorNum];
            _rowPitch = (uint)Align(Size * 4, device.Desc.memoryAlignment.uploadBufferTextureRow);
            _slicePitch = (uint)Align(_rowPitch * Size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
            for (var i = 0; i < colorNum; i++)
            {
                _colors[i] = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture2D(TextureUsageBits.COLOR_ATTACHMENT, Format.RGBA8_UNORM));
                ColorViews[i] = _colors[i].CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
                _readbacks[i] = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = _slicePitch });
            }

            DepthStencilFormat = depthStencilFormat;
            if (depthStencilFormat != Format.UNKNOWN)
            {
                _depthStencil = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture2D(TextureUsageBits.DEPTH_STENCIL_ATTACHMENT, depthStencilFormat));
                DepthStencilView = _depthStencil.CreateView(TextureView.DEPTH_STENCIL_ATTACHMENT, depthStencilFormat);
            }
        }

        public NriDescriptor[] ColorViews { get; }
        public NriDescriptor? DepthStencilView { get; }
        public Format DepthStencilFormat { get; }
        public bool HasStencil => DepthStencilFormat != Format.UNKNOWN && Nri.GetFormatProps(DepthStencilFormat).isStencil != 0;

        /// <summary>
        /// Records, submits and waits; returns one image per colour attachment. With
        /// <paramref name="submit"/> false the command buffer is only recorded (a control for a
        /// layer that checks at record time) and the result is empty.
        /// </summary>
        public Image[] Render(Action<NriCommandBuffer> inside, Action<NriCommandBuffer>? before = null, NriDescriptorPool? pool = null, float depthClear = 1, byte stencilClear = 0, bool submit = true)
        {
            var queue = _device.GetQueue(QueueType.GRAPHICS);
            using var allocator = queue.CreateCommandAllocator();
            using var cmd = allocator.CreateCommandBuffer();
            using var fence = _device.CreateFence();

            cmd.Begin(pool);
            before?.Invoke(cmd);

            var toAttachments = _colors.Select(t => new NriTextureBarrier(t, Undefined, ColorAttachment)).ToList();
            if (_depthStencil is not null) toAttachments.Add(new NriTextureBarrier(_depthStencil, Undefined, DepthStencilAttachment));
            cmd.Barrier([], [], toAttachments.ToArray());

            var colorClear = new ClearValue();
            colorClear.color.f = Black.ToFloat();
            var colors = ColorViews.Select(v => new NriAttachment(v, colorClear, LoadOp.CLEAR, StoreOp.STORE)).ToArray();
            NriAttachment depth = default, stencil = default;
            if (DepthStencilView is not null)
            {
                var depthStencilClear = new ClearValue();
                depthStencilClear.depthStencil = new DepthStencil { depth = depthClear, stencil = stencilClear };
                depth = new NriAttachment(DepthStencilView, depthStencilClear, LoadOp.CLEAR, StoreOp.STORE);
                if (HasStencil) stencil = depth;
            }

            cmd.BeginRendering(colors, depth, stencil);
            cmd.SetViewport(new Viewport { x = 0, y = 0, width = Size, height = Size, depthMin = 0, depthMax = 1 });
            cmd.SetScissor(new Rect { x = 0, y = 0, width = Size, height = Size });
            inside(cmd);
            cmd.EndRendering();

            cmd.Barrier([], [], _colors.Select(t => new NriTextureBarrier(t, ColorAttachment, TextureCopySource)).ToArray());
            for (var i = 0; i < _colors.Length; i++)
            {
                cmd.ReadbackTextureToBuffer(
                    _readbacks[i],
                    new TextureDataLayoutDesc { offset = 0, rowPitch = _rowPitch, slicePitch = _slicePitch },
                    _colors[i],
                    new TextureRegionDesc { width = Size, height = Size, depth = 1, planes = PlaneBits.COLOR });
            }
            cmd.End();
            if (!submit) return [];

            queue.Submit(cmd, fence, 1);
            Wait(fence, 1);

            var images = new Image[_colors.Length];
            for (var i = 0; i < _colors.Length; i++)
            {
                var mapped = _readbacks[i].Map();
                var bytes = new byte[Size * Size * 4];
                for (var y = 0; y < Size; y++)
                    mapped.Slice((int)(y * _rowPitch), Size * 4).CopyTo(bytes.AsSpan(y * Size * 4, Size * 4));
                _readbacks[i].Unmap();
                images[i] = new Image(bytes);
            }
            return images;
        }

        public void Dispose()
        {
            DepthStencilView?.Dispose();
            _depthStencil?.Dispose();
            foreach (var view in ColorViews) view.Dispose();
            foreach (var texture in _colors) texture.Dispose();
            foreach (var buffer in _readbacks) buffer.Dispose();
        }

        private static TextureDesc Texture2D(TextureUsageBits usage, Format format) => new()
        {
            type = TextureType.TEXTURE_2D, usage = usage, format = format,
            width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        };
    }

    internal static AccessLayoutStage DepthStencilAttachment => new() { access = AccessBits.DEPTH_STENCIL_ATTACHMENT, layout = Layout.DEPTH_STENCIL_ATTACHMENT, stages = StageBits.DEPTH_STENCIL_ATTACHMENT };

    // ---------------------------------------------------------------- harness: vertices, buffers, pipelines

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Vector2(float x, float y)
    {
        public readonly float X = x, Y = y;
    }

    /// <summary>The interleaved vertex of Shaders/DrawColor.hlsl: float2 clip-space position, RGBA8_UNORM colour.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ColorVertex(float x, float y, Rgba color)
    {
        public const uint Stride = 12;
        public readonly float X = x, Y = y;
        public readonly uint Color = color.Packed;
    }

    /// <summary>The per-instance record of Shaders/DrawInstanced.hlsl: float2 clip-space offset, RGBA8_UNORM colour.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct InstanceData(float x, float y, Rgba color)
    {
        public const ushort Stride = 12;
        public readonly float X = x, Y = y;
        public readonly uint Color = color.Packed;
    }

    /// <summary>Clip-space x of pixel column <paramref name="px"/> of the 64x64 target.</summary>
    internal static float ClipX(float px) => px / 32f - 1f;

    /// <summary>Clip-space y of pixel row <paramref name="py"/> (y down) of the 64x64 target.</summary>
    internal static float ClipY(float py) => 1f - py / 32f;

    /// <summary>A quad's four vertices: top-left, top-right, bottom-left, bottom-right, one colour.</summary>
    internal static ColorVertex[] Quad(PixelRect r, Rgba color) =>
    [
        new(ClipX(r.X0), ClipY(r.Y0), color),
        new(ClipX(r.X1), ClipY(r.Y0), color),
        new(ClipX(r.X0), ClipY(r.Y1), color),
        new(ClipX(r.X1), ClipY(r.Y1), color),
    ];

    /// <summary>The six indices of the quad whose four vertices start at <paramref name="first"/>.</summary>
    internal static uint[] QuadIndices(uint first) => [first, first + 1, first + 2, first + 3, first + 2, first + 1];

    /// <summary>Quads for the given rectangles, four vertices each, coloured as <see cref="QuadrantColors"/>.</summary>
    internal static ColorVertex[] QuadrantQuads(PixelRect[] rects) => rects.SelectMany((r, i) => Quad(r, QuadrantColors[i])).ToArray();

    /// <summary>Quads for the given rectangles as triangle lists, six vertices each, coloured as <see cref="QuadrantColors"/>.</summary>
    internal static ColorVertex[] QuadrantTriangles(PixelRect[] rects) =>
        rects.SelectMany((r, i) =>
        {
            var q = Quad(r, QuadrantColors[i]);
            return QuadIndices(0).Select(index => q[index]);
        }).ToArray();

    /// <summary>Shaders/DrawColor.hlsl's input from one interleaved stream in slot 0.</summary>
    internal static NriVertexInputDesc InterleavedInput => new()
    {
        Attributes =
        [
            new NriVertexAttributeDesc("POSITION", 0, Location: 0, Offset: 0, Format.RG32_SFLOAT, StreamIndex: 0),
            new NriVertexAttributeDesc("COLOR", 0, Location: 1, Offset: 8, Format.RGBA8_UNORM, StreamIndex: 0),
        ],
        Streams = [new VertexStreamDesc { bindingSlot = 0, stepRate = VertexStreamStepRate.PER_VERTEX, stride = (ushort)ColorVertex.Stride }],
    };

    /// <summary>
    /// A host-visible buffer holding <paramref name="data"/> after <paramref name="leadingBytes"/>
    /// bytes of 0xCD. Written through the map before any GPU use, so it needs no barrier.
    /// </summary>
    internal static NriBuffer Upload<T>(NriDevice device, ReadOnlySpan<T> data, BufferUsageBits usage, int leadingBytes = 0) where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(data);
        var buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = (ulong)(leadingBytes + bytes.Length), usage = usage });
        var mapped = buffer.Map();
        mapped[..leadingBytes].Fill(0xCD);
        bytes.CopyTo(mapped[leadingBytes..]);
        buffer.Unmap();
        return buffer;
    }

    internal static NriBuffer Upload<T>(NriDevice device, T[] data, BufferUsageBits usage, int leadingBytes = 0) where T : unmanaged =>
        Upload(device, (ReadOnlySpan<T>)data, usage, leadingBytes);

    /// <summary>A layout with nothing in it, for vertex and pixel shaders.</summary>
    internal static NriPipelineLayout CreateLayout(NriDevice device) =>
        device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });

    /// <summary>A pipeline of <paramref name="shader"/>.vs and .ps over one RGBA8 attachment, no blending, no depth, culling off.</summary>
    internal static NriPipeline CreateColorPipeline(NriDevice device, NriPipelineLayout layout, string shader, NriVertexInputDesc? input,
        Topology topology = Topology.TRIANGLE_LIST, PrimitiveRestart restart = PrimitiveRestart.DISABLED) =>
        device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            VertexInput = input,
            InputAssembly = new InputAssemblyDesc { topology = topology, primitiveRestart = restart },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE },
            OutputMerger = new NriOutputMergerDesc { Colors = [new ColorAttachmentDesc { format = Format.RGBA8_UNORM, colorWriteMask = ColorWriteBits.RGBA }] },
            Shaders =
            [
                new NriShaderDesc(StageBits.VERTEX_SHADER, PipelineTests.Bytecode(device, shader + ".vs"), "vsMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, PipelineTests.Bytecode(device, shader + ".ps"), "psMain"),
            ],
        });
}
