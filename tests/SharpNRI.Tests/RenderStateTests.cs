// Dynamic render state and depth-stencil attachments: CmdSetBlendConstants, CmdSetStencilReference
// (one reference and separate front and back ones), CmdSetDepthBias, CmdSetDepthBounds and
// CmdClearAttachments on colour and depth attachments, each changed between draws of one render
// pass so that the image shows the state each draw ran with.
//
// Every draw is a rectangle of Shaders/DrawRect.hlsl, placed, coloured and given a depth by root
// constants; rendering and the pixel comparison go through DrawTests' harness.
//
// The stencil-only clear runs in a child process (this test executable started again with
// SHARPNRI_TEST_CHILD set; the module initializer below runs it and exits before xunit starts): it
// killed the process before patch 0025, and a death there must fail its test, not end the run.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;
using static SharpNRI.Tests.DrawTests;
using static SharpNRI.Tests.GpuWork;

namespace SharpNRI.Tests;

public sealed class RenderStateTests
{
    private static readonly PixelRect Full = new(0, 0, Canvas.Size, Canvas.Size);
    private static readonly PixelRect Left = new(4, 4, 28, 60);
    private static readonly PixelRect Right = new(36, 4, 60, 60);

    /// <summary>
    /// Blending CONSTANT_COLOR x source + ZERO x destination of a white source writes the blend
    /// constants. Two draws in one pass with SetBlendConstants between them write two colours:
    /// (0.2, 0.4, 0.6) on the left, (1.0, 0.6, 0.2) on the right, within 1 of 255 times each.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Blend_constants_set_between_draws_are_what_each_draw_blends_with(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var layout = CreateRectLayout(device);
        using var pipeline = CreateRectPipeline(device, layout, color: new ColorAttachmentDesc
        {
            format = Format.RGBA8_UNORM,
            blendEnabled = 1,
            colorBlend = new BlendDesc { srcFactor = BlendFactor.CONSTANT_COLOR, dstFactor = BlendFactor.ZERO, op = BlendOp.ADD },
            alphaBlend = new BlendDesc { srcFactor = BlendFactor.ONE, dstFactor = BlendFactor.ZERO, op = BlendOp.ADD },
            colorWriteMask = ColorWriteBits.RGBA,
        });
        var first = new Color32f { x = 0.2f, y = 0.4f, z = 0.6f, w = 1 };
        var second = new Color32f { x = 1.0f, y = 0.6f, z = 0.2f, w = 1 };

        using var canvas = new Canvas(device);
        var image = canvas.Render(cmd =>
        {
            Bind(cmd, layout, pipeline);
            cmd.SetBlendConstants(first);
            DrawRect(cmd, Left, White);
            cmd.SetBlendConstants(second);
            DrawRect(cmd, Right, White);
        })[0];

        image.Expect((x, y) => Left.Contains(x, y) ? new Rgba(first) : Right.Contains(x, y) ? new Rgba(second) : Black, "two blend constants", tolerance: 1);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The stencil attachment cleared to 5, a pipeline testing EQUAL: the quad in quadrant 0 drawn
    /// with reference 5 passes, the one in quadrant 1 with reference 6 does not. A second pipeline
    /// (ALWAYS, REPLACE, colour writes off) with reference 9 writes 9 under quadrant 2's quad; a
    /// full-target draw with EQUAL and reference 9 then colours exactly that quad.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_stencil_reference_is_what_EQUAL_compares_and_REPLACE_writes(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var format = RequireStencilFormat(device);

        using var layout = CreateRectLayout(device);
        using var equal = CreateRectPipeline(device, layout, format, stencil: Both(StencilTest(CompareOp.EQUAL)));
        using var replace = CreateRectPipeline(device, layout, format,
            stencil: Both(new StencilDesc { compareOp = CompareOp.ALWAYS, failOp = StencilOp.KEEP, passOp = StencilOp.REPLACE, depthFailOp = StencilOp.KEEP, writeMask = 0xFF, compareMask = 0xFF }),
            color: ColorTarget(ColorWriteBits.NONE));

        using var canvas = new Canvas(device, depthStencilFormat: format);
        var image = canvas.Render(cmd =>
        {
            Bind(cmd, layout, equal);
            cmd.SetStencilReference(5, 5);
            DrawRect(cmd, Quadrants[0], Red);
            cmd.SetStencilReference(6, 6);
            DrawRect(cmd, Quadrants[1], Green);

            cmd.SetPipeline(replace);
            cmd.SetStencilReference(9, 9);
            DrawRect(cmd, Quadrants[2], White);

            cmd.SetPipeline(equal);
            cmd.SetStencilReference(9, 9);
            DrawRect(cmd, Full, Blue);
        }, stencilClear: 5)[0];

        image.ExpectQuadrants([Red, null, Blue, null], $"stencil EQUAL 5, EQUAL 6, REPLACE 9, EQUAL 9 ({format})");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// SetStencilReference(front, back) with culling off: against a stencil of 5 and EQUAL, the
    /// clockwise (front-facing) rectangle on the left and the mirrored, counter-clockwise
    /// (back-facing) one on the right are drawn red with references (5, 6), then green with (6, 5).
    /// Each face passes with its own reference only: the left stays red, the right turns green.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Front_and_back_stencil_references_apply_to_their_own_faces(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.independentFrontAndBackStencilReferenceAndMasks == 0,
            $"{backend}: features.independentFrontAndBackStencilReferenceAndMasks = 0; the back reference is the front one");
        var format = RequireStencilFormat(device);

        using var layout = CreateRectLayout(device);
        using var equal = CreateRectPipeline(device, layout, format, stencil: Both(StencilTest(CompareOp.EQUAL)));

        using var canvas = new Canvas(device, depthStencilFormat: format);
        var image = canvas.Render(cmd =>
        {
            Bind(cmd, layout, equal);
            cmd.SetStencilReference(5, 6);
            DrawRect(cmd, Left, Red);
            DrawRect(cmd, Right, Red, mirrored: true);
            cmd.SetStencilReference(6, 5);
            DrawRect(cmd, Left, Green);
            DrawRect(cmd, Right, Green, mirrored: true);
        }, stencilClear: 5)[0];

        image.Expect((x, y) => Left.Contains(x, y) ? Red : Right.Contains(x, y) ? Green : Black, $"front (left) and back (right) references ({format})");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A D32_SFLOAT attachment cleared to 0.5, rectangles at depth 0.5 tested LESS, in a pipeline
    /// whose static depth bias is -1000 (a bias in the pipeline is what makes it dynamic on both
    /// backends). Four strips, left to right: right after SetPipeline the pipeline's -1000 lets the
    /// draw pass; SetDepthBias(0) fails it (0.5 is not less than 0.5); SetDepthBias(-1000) passes;
    /// SetDepthBias(+1000) fails.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void SetDepthBias_overrides_the_pipeline_bias_per_draw(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.dynamicDepthBias == 0, $"{backend}: features.dynamicDepthBias = 0");
        RequireDepthFormat(device, Format.D32_SFLOAT);

        PixelRect[] strips = [new(4, 4, 12, 60), new(20, 4, 28, 60), new(36, 4, 44, 60), new(52, 4, 60, 60)];
        using var layout = CreateRectLayout(device);
        using var pipeline = CreateRectPipeline(device, layout, Format.D32_SFLOAT,
            depth: new DepthAttachmentDesc { compareOp = CompareOp.LESS, write = 0 },
            bias: new DepthBiasDesc { constant = -1000, clamp = 0, slope = 0 });

        using var canvas = new Canvas(device, depthStencilFormat: Format.D32_SFLOAT);
        var image = canvas.Render(cmd =>
        {
            Bind(cmd, layout, pipeline);
            DrawRect(cmd, strips[0], Red, 0.5f);
            cmd.SetDepthBias(new DepthBiasDesc { constant = 0, clamp = 0, slope = 0 });
            DrawRect(cmd, strips[1], Red, 0.5f);
            cmd.SetDepthBias(new DepthBiasDesc { constant = -1000, clamp = 0, slope = 0 });
            DrawRect(cmd, strips[2], Red, 0.5f);
            cmd.SetDepthBias(new DepthBiasDesc { constant = 1000, clamp = 0, slope = 0 });
            DrawRect(cmd, strips[3], Red, 0.5f);
        }, depthClear: 0.5f)[0];

        image.Expect((x, y) => strips[0].Contains(x, y) || strips[2].Contains(x, y) ? Red : Black, "pipeline bias -1000, then 0, -1000, +1000");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The other half of the pair above: NRI makes the depth bias dynamic only for a pipeline
    /// created with a non-zero RasterizationDesc.depthBias (VK_DYNAMIC_STATE_DEPTH_BIAS in
    /// PipelineVK.hpp, D3D12_PIPELINE_STATE_FLAG_DYNAMIC_DEPTH_BIAS in PipelineD3D12.hpp), so
    /// SetDepthBias on a pipeline without one is undefined, and both layers say so: the Khronos
    /// layer at the draw (VUID-vkCmdDraw-None-08608), the D3D12 debug layer at RSSetDepthBias
    /// (1366). Measured: the draw then ignores the bias on Vulkan and applies it on D3D12, so no
    /// pixel is asserted.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void SetDepthBias_on_a_pipeline_created_without_a_bias_is_reported_by_both_layers(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.dynamicDepthBias == 0, $"{backend}: features.dynamicDepthBias = 0");
        RequireDepthFormat(device, Format.D32_SFLOAT);

        using var layout = CreateRectLayout(device);
        using var pipeline = CreateRectPipeline(device, layout, Format.D32_SFLOAT, depth: new DepthAttachmentDesc { compareOp = CompareOp.LESS, write = 0 });

        using var canvas = new Canvas(device, depthStencilFormat: Format.D32_SFLOAT);
        canvas.Render(cmd =>
        {
            Bind(cmd, layout, pipeline);
            cmd.SetDepthBias(new DepthBiasDesc { constant = -1000, clamp = 0, slope = 0 });
            DrawRect(cmd, Left, Red, 0.5f);
        }, depthClear: 0.5f);

        List<NriMessage> errors;
        lock (messages) errors = messages.Where(m => m.IsError).ToList();
        foreach (var m in errors) GpuWork.Record(m.Text);
        Func<NriMessage, bool> expected = backend.Api == GraphicsAPI.VK
            ? m => m.Text.Contains("VUID-vkCmdDraw-None-08608", StringComparison.Ordinal)
            : m => m.Text.Contains("[1366]", StringComparison.Ordinal) && m.Text.Contains("D3D12_PIPELINE_STATE_FLAG_DYNAMIC_DEPTH_BIAS", StringComparison.Ordinal);
        Assert.Contains(errors, m => expected(m));
        Assert.All(errors, m => Assert.True(expected(m), $"unexpected error: {m.Text}"));
    }

    /// <summary>
    /// Depth 0.3 is written on the left half and 0.7 on the right (colour writes off); then two
    /// full-target draws with the depth bounds test, red with SetDepthBounds(0, 0.5) and green with
    /// SetDepthBounds(0.5, 1). The test reads the stored depth, so each half takes the colour of the
    /// draw whose bounds hold its depth.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_depth_bounds_test_keeps_fragments_whose_stored_depth_is_within_the_bounds(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.depthBoundsTest == 0, $"{backend}: features.depthBoundsTest = 0");
        RequireDepthFormat(device, Format.D32_SFLOAT);

        using var layout = CreateRectLayout(device);
        using var write = CreateRectPipeline(device, layout, Format.D32_SFLOAT,
            depth: new DepthAttachmentDesc { compareOp = CompareOp.ALWAYS, write = 1 },
            color: ColorTarget(ColorWriteBits.NONE));
        using var bounded = CreateRectPipeline(device, layout, Format.D32_SFLOAT,
            depth: new DepthAttachmentDesc { compareOp = CompareOp.ALWAYS, write = 0, boundsTest = 1 });

        var leftHalf = new PixelRect(0, 0, 32, 64);
        var rightHalf = new PixelRect(32, 0, 64, 64);
        using var canvas = new Canvas(device, depthStencilFormat: Format.D32_SFLOAT);
        var image = canvas.Render(cmd =>
        {
            Bind(cmd, layout, write);
            DrawRect(cmd, leftHalf, White, 0.3f);
            DrawRect(cmd, rightHalf, White, 0.7f);

            cmd.SetPipeline(bounded);
            cmd.SetDepthBounds(0, 0.5f);
            DrawRect(cmd, Full, Red, 0.5f);
            cmd.SetDepthBounds(0.5f, 1);
            DrawRect(cmd, Full, Green, 0.5f);
        })[0];

        image.Expect((x, _) => x < 32 ? Red : Green, "bounds [0, 0.5] then [0.5, 1] over stored 0.3 | 0.7");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Mid-pass, after a red full-target draw, ClearAttachments clears the colour attachment to
    /// green in two rectangles: only those take the clear colour.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void ClearAttachments_clears_a_colour_attachment_only_inside_the_rects(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.rectColorClears == 0, $"{backend}: features.rectColorClears = 0");

        Rect[] rects = [new Rect { x = 8, y = 8, width = 16, height = 16 }, new Rect { x = 40, y = 32, width = 16, height = 24 }];
        using var layout = CreateRectLayout(device);
        using var pipeline = CreateRectPipeline(device, layout);

        using var canvas = new Canvas(device);
        var image = canvas.Render(cmd =>
        {
            Bind(cmd, layout, pipeline);
            DrawRect(cmd, Full, Red);
            cmd.ClearAttachments([ColorClear(Green, 0)], rects);
        })[0];

        image.Expect((x, y) => rects.Any(r => x >= r.x && x < r.x + r.width && y >= r.y && y < r.y + r.height) ? Green : Red, "two rect clears over red");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A depth clear mid-pass is what the next draw tests against. Depth 0.5 is written everywhere
    /// with a red draw; ClearAttachments sets the left half to 0.25; a green full-target draw at
    /// depth 0.4, LESS, then fails on the left, against the new 0.25 (it would pass against 0.5),
    /// and passes on the right, where the depth is still 0.5.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void ClearAttachments_on_the_depth_plane_is_what_the_next_draw_tests_against(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.rectDepthStencilClears == 0, $"{backend}: features.rectDepthStencilClears = 0");
        RequireDepthFormat(device, Format.D32_SFLOAT);

        using var layout = CreateRectLayout(device);
        using var pipeline = CreateRectPipeline(device, layout, Format.D32_SFLOAT, depth: new DepthAttachmentDesc { compareOp = CompareOp.LESS, write = 1 });

        var depthClear = new ClearAttachmentDesc { planes = PlaneBits.DEPTH };
        depthClear.value.depthStencil = new DepthStencil { depth = 0.25f };
        using var canvas = new Canvas(device, depthStencilFormat: Format.D32_SFLOAT);
        var image = canvas.Render(cmd =>
        {
            Bind(cmd, layout, pipeline);
            DrawRect(cmd, Full, Red, 0.5f);
            cmd.ClearAttachments([depthClear], [new Rect { x = 0, y = 0, width = 32, height = 64 }]);
            DrawRect(cmd, Full, Green, 0.4f);
        })[0];

        image.Expect((x, _) => x < 32 ? Red : Green, "depth 0.5, left half cleared to 0.25, then a draw at 0.4");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A stencil clear mid-pass is what the next draw compares: the stencil is cleared to 5 at
    /// BeginRendering, ClearAttachments sets it to 7 under quadrant 1's quad, and a full-target draw
    /// testing EQUAL with reference 7 colours exactly that quad. The depth-stencil view is passed
    /// as both RenderingDesc::depth and ::stencil, as Canvas does; the stencil-only spelling is
    /// <see cref="ClearAttachments_in_a_stencil_only_pass_clears_the_stencil"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void ClearAttachments_on_the_stencil_plane_is_what_the_next_draw_compares(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.rectDepthStencilClears == 0, $"{backend}: features.rectDepthStencilClears = 0");
        var format = RequireStencilFormat(device);

        using var layout = CreateRectLayout(device);
        using var equal = CreateRectPipeline(device, layout, format, stencil: Both(StencilTest(CompareOp.EQUAL)));

        var stencilClear = new ClearAttachmentDesc { planes = PlaneBits.STENCIL };
        stencilClear.value.depthStencil = new DepthStencil { stencil = 7 };
        var quad = Quadrants[1];
        using var canvas = new Canvas(device, depthStencilFormat: format);
        var image = canvas.Render(cmd =>
        {
            cmd.ClearAttachments([stencilClear], [new Rect { x = (short)quad.X0, y = (short)quad.Y0, width = (ushort)(quad.X1 - quad.X0), height = (ushort)(quad.Y1 - quad.Y0) }]);
            Bind(cmd, layout, equal);
            cmd.SetStencilReference(7, 7);
            DrawRect(cmd, Full, Green);
        }, stencilClear: 5)[0];

        image.ExpectQuadrants([null, Green, null, null], $"stencil 5, quadrant 1 cleared to 7, EQUAL 7 ({format})");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A depth-stencil view passed as RenderingDesc::depth alone, LoadOp CLEAR with a clear value
    /// whose stencil is 5, over a stencil of 9: the stencil is cleared to 5 on both backends, as
    /// NRIDescs.h allows ("depth" may be treated as depth-stencil) and NRI's Vulkan backend always
    /// did (pStencilAttachment points at the depth attachment when the format has stencil). A draw
    /// testing EQUAL 5 then colours the left half, and one testing EQUAL 9 nothing. Before patch 0028
    /// NRI's D3D12 backend took the stencil clear from RenderingDesc::stencil only (N4): the stencil
    /// stayed 9, so the right half was drawn and the left was not.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_depth_stencil_view_passed_as_depth_alone_clears_its_stencil_too(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var format = RequireStencilFormat(device);

        using var layout = CreateRectLayout(device);
        using var equal = CreateRectPipeline(device, layout, format, stencil: Both(StencilTest(CompareOp.EQUAL)));
        TextureDesc Target(TextureUsageBits usage, Format f) => new() { type = TextureType.TEXTURE_2D, usage = usage, format = f, width = Canvas.Size, height = Canvas.Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1 };
        using var color = device.CreateCommittedTexture(MemoryLocation.DEVICE, Target(TextureUsageBits.COLOR_ATTACHMENT, Format.RGBA8_UNORM));
        using var colorView = color.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var depthStencil = device.CreateCommittedTexture(MemoryLocation.DEVICE, Target(TextureUsageBits.DEPTH_STENCIL_ATTACHMENT, format));
        using var depthStencilView = depthStencil.CreateView(TextureView.DEPTH_STENCIL_ATTACHMENT, format);
        var rowPitch = (uint)Align(Canvas.Size * 4, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)Align(rowPitch * Canvas.Size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        var black = new ClearValue();
        black.color.f = Black.ToFloat();
        var nine = new ClearValue();
        nine.depthStencil = new DepthStencil { depth = 1, stencil = 9 };
        var five = new ClearValue();
        five.depthStencil = new DepthStencil { depth = 1, stencil = 5 };
        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(color, Undefined, ColorAttachment), new NriTextureBarrier(depthStencil, Undefined, DepthStencilAttachment)]);
            var both = new NriAttachment(depthStencilView, nine, LoadOp.CLEAR, StoreOp.STORE);
            cmd.BeginRendering([new NriAttachment(colorView, black, LoadOp.CLEAR, StoreOp.STORE)], both, both);
            cmd.EndRendering();
            cmd.BeginRendering([new NriAttachment(colorView, black, LoadOp.CLEAR, StoreOp.STORE)], new NriAttachment(depthStencilView, five, LoadOp.CLEAR, StoreOp.STORE));
            cmd.SetViewport(new Viewport { width = Canvas.Size, height = Canvas.Size, depthMax = 1 });
            cmd.SetScissor(new Rect { width = Canvas.Size, height = Canvas.Size });
            Bind(cmd, layout, equal);
            cmd.SetStencilReference(5, 5);
            DrawRect(cmd, new PixelRect(0, 0, 32, Canvas.Size), Red);
            cmd.SetStencilReference(9, 9);
            DrawRect(cmd, new PixelRect(32, 0, Canvas.Size, Canvas.Size), Green);
            cmd.EndRendering();
            cmd.Barrier(new NriTextureBarrier(color, ColorAttachment, TextureCopySource));
            cmd.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = rowPitch, slicePitch = slicePitch }, color,
                new TextureRegionDesc { width = Canvas.Size, height = Canvas.Size, depth = 1, planes = PlaneBits.COLOR });
        });

        var mapped = readback.Map();
        var bytes = new byte[Canvas.Size * Canvas.Size * 4];
        for (var y = 0; y < Canvas.Size; y++) mapped.Slice(y * (int)rowPitch, Canvas.Size * 4).CopyTo(bytes.AsSpan(y * Canvas.Size * 4));
        readback.Unmap();
        new Image(bytes).Expect((x, _) => x < 32 ? Red : Black, $"stencil 9, then the view as 'depth' alone cleared to 5: EQUAL 5 left, EQUAL 9 right ({format})");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// With two colour attachments, ClearAttachments' colorAttachmentIndex picks the target: index 1
    /// is cleared to green whole, then index 0 to blue in its top half. Attachment 0 is blue on top
    /// and still black below; attachment 1 is green everywhere.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void ClearAttachments_clears_the_colour_attachment_its_index_names(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.rectColorClears == 0, $"{backend}: features.rectColorClears = 0");
        Assert.SkipWhen(device.Desc.shaderStage.fragment.attachmentMaxNum < 2, $"{backend}: shaderStage.fragment.attachmentMaxNum = {device.Desc.shaderStage.fragment.attachmentMaxNum}");

        using var canvas = new Canvas(device, colorNum: 2);
        var images = canvas.Render(cmd =>
        {
            cmd.ClearAttachments([ColorClear(Green, 1)]);
            cmd.ClearAttachments([ColorClear(Blue, 0)], [new Rect { x = 0, y = 0, width = 64, height = 32 }]);
        });

        images[0].Expect((_, y) => y < 32 ? Blue : Black, "attachment 0: index 0 cleared in its top half");
        images[1].Expect((_, _) => Green, "attachment 1: index 1 cleared whole");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- rectangles

    /// <summary>Shaders/DrawRect.hlsl's 48 bytes of root constants.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RectConstants
    {
        public float X0, Y0, X1, Y1;
        public Color32f Color;
        public float Depth, Unused0, Unused1, Unused2;
    }

    /// <summary>One rectangle: six vertices, root constants for its pixels, colour and depth. Mirrored swaps x0 and x1, reversing the winding.</summary>
    private static void DrawRect(NriCommandBuffer cmd, PixelRect r, Rgba color, float depth = 0, bool mirrored = false)
    {
        var constants = new RectConstants
        {
            X0 = mirrored ? r.X1 : r.X0, Y0 = r.Y0, X1 = mirrored ? r.X0 : r.X1, Y1 = r.Y1,
            Color = color.ToFloat(),
            Depth = depth,
        };
        cmd.SetRootConstants(0, constants);
        cmd.Draw(6);
    }

    private static void Bind(NriCommandBuffer cmd, NriPipelineLayout layout, NriPipeline pipeline)
    {
        cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
        cmd.SetPipeline(pipeline);
    }

    private static NriPipelineLayout CreateRectLayout(NriDevice device) => device.CreatePipelineLayout(new NriPipelineLayoutDesc
    {
        RootConstants = [new RootConstantDesc { registerIndex = 0, size = (uint)Marshal.SizeOf<RectConstants>(), shaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER }],
        ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER,
    });

    /// <summary>A Shaders/DrawRect.hlsl pipeline over one colour attachment and, when given, a depth-stencil format; culling off.</summary>
    private static NriPipeline CreateRectPipeline(NriDevice device, NriPipelineLayout layout, Format depthStencilFormat = Format.UNKNOWN,
        DepthAttachmentDesc depth = default, StencilAttachmentDesc stencil = default, ColorAttachmentDesc? color = null, DepthBiasDesc bias = default) =>
        device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            InputAssembly = new InputAssemblyDesc { topology = Topology.TRIANGLE_LIST },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE, depthBias = bias },
            OutputMerger = new NriOutputMergerDesc
            {
                Colors = [color ?? ColorTarget(ColorWriteBits.RGBA)],
                Depth = depth,
                Stencil = stencil,
                DepthStencilFormat = depthStencilFormat,
            },
            Shaders =
            [
                new NriShaderDesc(StageBits.VERTEX_SHADER, PipelineTests.Bytecode(device, "DrawRect.vs"), "vsMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, PipelineTests.Bytecode(device, "DrawRect.ps"), "psMain"),
            ],
        });

    private static ColorAttachmentDesc ColorTarget(ColorWriteBits mask) => new() { format = Format.RGBA8_UNORM, colorWriteMask = mask };

    private static StencilDesc StencilTest(CompareOp compare) =>
        new() { compareOp = compare, failOp = StencilOp.KEEP, passOp = StencilOp.KEEP, depthFailOp = StencilOp.KEEP, writeMask = 0, compareMask = 0xFF };

    private static StencilAttachmentDesc Both(StencilDesc face) => new() { front = face, back = face };

    private static ClearAttachmentDesc ColorClear(Rgba color, byte index)
    {
        var desc = new ClearAttachmentDesc { planes = PlaneBits.COLOR, colorAttachmentIndex = index };
        desc.value.color.f = color.ToFloat();
        return desc;
    }

    /// <summary>The first of D32_SFLOAT_S8_UINT and D24_UNORM_S8_UINT the device can attach; skips naming both when neither.</summary>
    private static Format RequireStencilFormat(NriDevice device)
    {
        foreach (var format in new[] { Format.D32_SFLOAT_S8_UINT, Format.D24_UNORM_S8_UINT })
            if ((device.GetFormatSupport(format) & FormatSupportBits.DEPTH_STENCIL_ATTACHMENT) != 0) return format;
        Assert.Skip($"{device.GraphicsApi}: neither D32_SFLOAT_S8_UINT nor D24_UNORM_S8_UINT has FormatSupportBits.DEPTH_STENCIL_ATTACHMENT");
        return Format.UNKNOWN;
    }

    private static void RequireDepthFormat(NriDevice device, Format format) =>
        Assert.SkipWhen((device.GetFormatSupport(format) & FormatSupportBits.DEPTH_STENCIL_ATTACHMENT) == 0,
            $"{device.GraphicsApi}: {format} lacks FormatSupportBits.DEPTH_STENCIL_ATTACHMENT");

    // ---------------------------------------------------------------- a stencil-only pass (patch 0025)

    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildBackendVariable = "SHARPNRI_TEST_CHILD_BACKEND";
    private const string StencilOnlyChild = "stencil-only-clear";

    /// <summary>
    /// ClearAttachments in a pass whose depth-stencil view is bound as RenderingDesc::stencil alone:
    /// the stencil is cleared to 5 at BeginRendering and ClearAttachments sets it to 7 under quadrant
    /// 1's quad; a second pass, the view loaded as depth and stencil, draws green where EQUAL 7
    /// passes, which is exactly that quad. Before patch 0025 NRI's D3D12 ClearAttachments took the
    /// view from the depth attachment, which a stencil-only pass does not have, and the process died
    /// with an access violation (N3); Vulkan cleared. The passes run in a child process.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void ClearAttachments_in_a_stencil_only_pass_clears_the_stencil(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var lines = RunChild(StencilOnlyChild, backend);
        var skip = lines.FirstOrDefault(l => l.StartsWith("SKIP ", StringComparison.Ordinal));
        Assert.SkipWhen(skip is not null, skip?[5..] ?? "");
        Assert.Contains($"RESULT {backend}: EQUAL 7 drew quadrant 1 and nothing else", lines);
    }

    /// <summary>Runs this executable as the named child for one backend; its stdout lines, after asserting it exited with 0.</summary>
    private static string[] RunChild(string name, Backend backend)
    {
        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location); // "dotnet SharpNRI.Tests.dll"
        start.Environment[ChildVariable] = name;
        start.Environment[ChildBackendVariable] = backend.Api.ToString();

        using var child = Process.Start(start)!;
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!child.WaitForExit(GpuWork.GpuTimeout))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"the {name} child did not finish within {GpuWork.GpuTimeout}");
        }
        child.WaitForExit();
        var lines = stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) GpuWork.Record(line);
        if (stderr.Result.Length != 0) GpuWork.Record("stderr: " + stderr.Result);
        Assert.True(child.ExitCode == 0, $"the {name} child ({backend}) exited with 0x{child.ExitCode:X8}; its last lines: " + string.Join(" | ", lines.TakeLast(3)));
        return lines;
    }

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunChildIfAsked()
    {
        if (Environment.GetEnvironmentVariable(ChildVariable) != StencilOnlyChild) return;
        int code;
        try
        {
            code = StencilOnlyClear(Enum.Parse<GraphicsAPI>(Environment.GetEnvironmentVariable(ChildBackendVariable)!));
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static int StencilOnlyClear(GraphicsAPI api)
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(api, messages);
        if (device.Desc.features.rectDepthStencilClears == 0)
        {
            Console.WriteLine($"SKIP {api}: features.rectDepthStencilClears = 0");
            return 0;
        }
        var format = new[] { Format.D32_SFLOAT_S8_UINT, Format.D24_UNORM_S8_UINT }.FirstOrDefault(f => (device.GetFormatSupport(f) & FormatSupportBits.DEPTH_STENCIL_ATTACHMENT) != 0);
        if (format == Format.UNKNOWN)
        {
            Console.WriteLine($"SKIP {api}: neither D32_SFLOAT_S8_UINT nor D24_UNORM_S8_UINT has FormatSupportBits.DEPTH_STENCIL_ATTACHMENT");
            return 0;
        }

        using var layout = CreateRectLayout(device);
        using var equal = CreateRectPipeline(device, layout, format, stencil: Both(StencilTest(CompareOp.EQUAL)));
        TextureDesc Target(TextureUsageBits usage, Format f) => new() { type = TextureType.TEXTURE_2D, usage = usage, format = f, width = Canvas.Size, height = Canvas.Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1 };
        using var color = device.CreateCommittedTexture(MemoryLocation.DEVICE, Target(TextureUsageBits.COLOR_ATTACHMENT, Format.RGBA8_UNORM));
        using var colorView = color.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var depthStencil = device.CreateCommittedTexture(MemoryLocation.DEVICE, Target(TextureUsageBits.DEPTH_STENCIL_ATTACHMENT, format));
        using var depthStencilView = depthStencil.CreateView(TextureView.DEPTH_STENCIL_ATTACHMENT, format);
        var rowPitch = (uint)GpuWork.Align(Canvas.Size * 4, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)GpuWork.Align(rowPitch * Canvas.Size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var cmd = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();
        var quad = Quadrants[1];
        var stencilClear = new ClearAttachmentDesc { planes = PlaneBits.STENCIL };
        stencilClear.value.depthStencil = new DepthStencil { stencil = 7 };
        var five = new ClearValue();
        five.depthStencil = new DepthStencil { depth = 1, stencil = 5 };
        var black = new ClearValue();
        black.color.f = Black.ToFloat();

        cmd.Begin();
        cmd.Barrier([], [], [new NriTextureBarrier(color, GpuWork.Undefined, GpuWork.ColorAttachment), new NriTextureBarrier(depthStencil, GpuWork.Undefined, DepthStencilAttachment)]);
        cmd.BeginRendering([], default, new NriAttachment(depthStencilView, five, LoadOp.CLEAR, StoreOp.STORE));
        cmd.SetViewport(new Viewport { width = Canvas.Size, height = Canvas.Size, depthMax = 1 });
        cmd.SetScissor(new Rect { width = Canvas.Size, height = Canvas.Size });
        cmd.ClearAttachments([stencilClear], [new Rect { x = (short)quad.X0, y = (short)quad.Y0, width = (ushort)(quad.X1 - quad.X0), height = (ushort)(quad.Y1 - quad.Y0) }]);
        cmd.EndRendering();
        var loaded = new NriAttachment(depthStencilView, default, LoadOp.LOAD, StoreOp.STORE);
        cmd.BeginRendering([new NriAttachment(colorView, black, LoadOp.CLEAR, StoreOp.STORE)], loaded, loaded);
        cmd.SetViewport(new Viewport { width = Canvas.Size, height = Canvas.Size, depthMax = 1 });
        cmd.SetScissor(new Rect { width = Canvas.Size, height = Canvas.Size });
        Bind(cmd, layout, equal);
        cmd.SetStencilReference(7, 7);
        DrawRect(cmd, Full, Green);
        cmd.EndRendering();
        cmd.Barrier(new NriTextureBarrier(color, GpuWork.ColorAttachment, GpuWork.TextureCopySource));
        cmd.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = rowPitch, slicePitch = slicePitch }, color,
            new TextureRegionDesc { width = Canvas.Size, height = Canvas.Size, depth = 1, planes = PlaneBits.COLOR });
        cmd.End();
        queue.Submit(cmd, fence, 1);
        var clock = Stopwatch.StartNew();
        while (fence.Value < 1)
        {
            if (clock.Elapsed > GpuWork.GpuTimeout) throw new TimeoutException("the submit did not finish");
            Thread.Sleep(1);
        }

        var mapped = readback.Map();
        var bytes = new byte[Canvas.Size * Canvas.Size * 4];
        for (var y = 0; y < Canvas.Size; y++) mapped.Slice(y * (int)rowPitch, Canvas.Size * 4).CopyTo(bytes.AsSpan(y * Canvas.Size * 4));
        readback.Unmap();
        new Image(bytes).ExpectQuadrants([null, Green, null, null], $"stencil 5, quadrant 1 cleared to 7 in a stencil-only pass, EQUAL 7 ({format})");
        foreach (var m in messages.Where(m => m.IsError)) Console.WriteLine($"FAIL {m}");
        if (messages.Any(m => m.IsError)) return 1;
        Console.WriteLine($"RESULT {api}: EQUAL 7 drew quadrant 1 and nothing else");
        return 0;
    }
}
