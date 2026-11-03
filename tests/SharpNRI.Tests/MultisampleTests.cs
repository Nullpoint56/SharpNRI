// Multisampled rendering and everything that turns its samples into pixels: CmdResolveTexture
// (whole texture, regions, every ResolveOp the device reports), the resolve an attachment does at
// EndRendering, programmable sample locations (CmdSetSampleLocations) and variable rate shading
// (CmdSetShadingRate, and a shading-rate attachment).
//
// The images are drawn so that every sample's value is known: MsaaTriangle.hlsl writes one colour
// into the samples a triangle covers, the clear colour stays in the others, so a resolved pixel is
// a known function of how many of its samples the triangle covers. MsaaPattern.hlsl covers every
// sample and writes a colour that encodes the pixel position, so a resolved region shows which
// pixels it came from, and a coarse-shaded block shows how many invocations shaded it.

using SharpNRI.Native;
using Xunit;
using static SharpNRI.Tests.GpuWork;

namespace SharpNRI.Tests;

public sealed class MultisampleTests
{
    private const ushort Size = 32;
    private const byte Samples = 4;

    // RGBA8 as bytes. The clear colour and MsaaTriangle.hlsl's colour differ in R, G and B, so every
    // mix of the two is distinguishable, and their component-wise minimum and maximum are neither.
    private static readonly byte[] ClearBytes = [200, 40, 120, 255];
    private static readonly byte[] TriangleBytes = [60, 220, 90, 255];

    private static readonly AccessLayoutStage ResolveSource = new() { access = AccessBits.RESOLVE_SOURCE, layout = Layout.RESOLVE_SOURCE, stages = StageBits.RESOLVE };
    private static readonly AccessLayoutStage ResolveDestination = new() { access = AccessBits.RESOLVE_DESTINATION, layout = Layout.RESOLVE_DESTINATION, stages = StageBits.RESOLVE };

    public static TheoryData<Backend, ResolveOp> GpuBackendsAndResolveOps
    {
        get
        {
            var data = new TheoryData<Backend, ResolveOp>();
            foreach (var api in new[] { GraphicsAPI.VK, GraphicsAPI.D3D12 })
            foreach (var op in new[] { ResolveOp.MIN, ResolveOp.MAX, ResolveOp.SAMPLE_ZERO })
                data.Add(new Backend(api), op);
            return data;
        }
    }

    /// <summary>
    /// CmdResolveTexture of a whole 4x texture with AVERAGE. Every resolved pixel is the average of
    /// samples that hold either the clear colour or the triangle's, so it is (k * triangle + (4 - k)
    /// * clear) / 4 for the number k of covered samples, in every channel with the same k: the
    /// corners are the clear colour exactly, the interior the triangle's exactly, and the edges have
    /// pixels with k = 1, 2 or 3, strictly between the two.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_4x_triangle_resolves_to_exact_interior_and_blended_edges(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultisampleColor(device, Format.RGBA8_UNORM);
        RequireResolveOp(device, device.Desc.resolve.command.color, ResolveOp.AVERAGE, "resolve.command.color");

        using var scene = new TriangleScene(device, Format.RGBA8_UNORM);
        using var msaa = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, samples: Samples);
        using var msaaView = msaa.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var resolved = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.SHADER_RESOURCE);
        using var readback = new TextureReadback(device, resolved, 4, (0, 0));

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(msaa, Undefined, ColorAttachment), new NriTextureBarrier(resolved, Undefined, ResolveDestination)]);
            scene.Draw(cmd, msaaView, Clear(ClearBytes));
            cmd.Barrier(new NriTextureBarrier(msaa, ColorAttachment, ResolveSource));
            cmd.ResolveTexture(resolved, null, msaa, null, ResolveOp.AVERAGE);
            cmd.Barrier(new NriTextureBarrier(resolved, ResolveDestination, TextureCopySource));
            readback.Record(cmd);
        });

        var pixels = readback.Read()[0];
        var counts = CoveredSampleCounts(pixels, Samples, out var misfits);
        var histogram = string.Join(", ", Enumerable.Range(0, Samples + 1).Select(k => $"{k}: {counts.Count(c => c == k)}"));
        Record($"{backend}: covered-sample histogram of {Size}x{Size} resolved pixels: {histogram}; {misfits.Count} pixel(s) fit no k");

        AssertPixel(pixels, 0, 0, ClearBytes, "top-left corner, outside the triangle");
        AssertPixel(pixels, Size - 1, Size - 1, ClearBytes, "bottom-right corner, outside the triangle");
        AssertPixel(pixels, Size / 2, Size / 2, TriangleBytes, "the centre, inside the triangle");
        Assert.True(misfits.Count == 0, $"pixels that are no k/4 mix of the clear and triangle colours: {string.Join("; ", misfits.Take(8))}");
        Assert.Contains(counts, k => k is > 0 and < Samples);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The resolve an attachment does at EndRendering (AttachmentDesc::resolveDst, resolveOp
    /// AVERAGE) gives the pixels CmdResolveTexture gives for the same samples: the same triangle is
    /// drawn once, resolved by the render pass into one texture and by the command into another.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_attachment_resolve_matches_ResolveTexture(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultisampleColor(device, Format.RGBA8_UNORM);
        RequireResolveOp(device, device.Desc.resolve.attachment.color, ResolveOp.AVERAGE, "resolve.attachment.color");
        RequireResolveOp(device, device.Desc.resolve.command.color, ResolveOp.AVERAGE, "resolve.command.color");

        using var scene = new TriangleScene(device, Format.RGBA8_UNORM);
        using var msaa = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, samples: Samples);
        using var msaaView = msaa.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var byPass = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT);
        using var byPassView = byPass.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var byCommand = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.SHADER_RESOURCE);
        using var passReadback = new TextureReadback(device, byPass, 4, (0, 0));
        using var commandReadback = new TextureReadback(device, byCommand, 4, (0, 0));

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [
                new NriTextureBarrier(msaa, Undefined, ColorAttachment),
                new NriTextureBarrier(byPass, Undefined, ColorAttachment),
                new NriTextureBarrier(byCommand, Undefined, ResolveDestination),
            ]);
            scene.Draw(cmd, msaaView, Clear(ClearBytes), resolveDst: byPassView);
            cmd.Barrier([], [], [
                new NriTextureBarrier(msaa, ColorAttachment, ResolveSource),
                new NriTextureBarrier(byPass, ColorAttachment, TextureCopySource),
            ]);
            cmd.ResolveTexture(byCommand, null, msaa, null, ResolveOp.AVERAGE);
            cmd.Barrier(new NriTextureBarrier(byCommand, ResolveDestination, TextureCopySource));
            passReadback.Record(cmd);
            commandReadback.Record(cmd);
        });

        var pass = passReadback.Read()[0];
        var command = commandReadback.Read()[0];
        var differing = Enumerable.Range(0, pass.Length).Where(i => pass[i] != command[i]).ToList();
        Record($"{backend}: {differing.Count} of {pass.Length} bytes differ between the attachment resolve and ResolveTexture");

        // Within 1: the two paths may round the average of four UNORM values differently.
        Assert.All(differing, i => Assert.True(Math.Abs(pass[i] - command[i]) <= 1, $"byte {i} (pixel {i / 4 % Size},{i / 4 / Size}): attachment resolve {pass[i]}, ResolveTexture {command[i]}"));
        var counts = CoveredSampleCounts(pass, Samples, out var misfits);
        Assert.Empty(misfits);
        Assert.Contains(counts, k => k is > 0 and < Samples);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// An attachment resolve into a view of one mip and layer of an array texture lands in that
    /// subresource and nowhere else: the destination has two mips and three layers, the view names
    /// mip 1 (the source's size) of layer 1, and every other subresource keeps its background. Before
    /// patch 0036 NRI's D3D12 backend computed the destination subresource with the layer count
    /// where the mip count belongs (EndRendering, fixed on NRI's main by 48d4a33): the resolve landed
    /// in subresource 4, mip 0 of layer 2, with "[1334] ... ResolveSubresourceRegion: ...
    /// subresource = [4], does not support the required access", and into a one-mip destination's
    /// layer 1 it was "[877] ... subresource index is out of bounds" and Close failed.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_attachment_resolve_lands_in_the_mip_and_layer_of_its_view(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultisampleColor(device, Format.RGBA8_UNORM);
        RequireResolveOp(device, device.Desc.resolve.attachment.color, ResolveOp.AVERAGE, "resolve.attachment.color");

        using var scene = new TriangleScene(device, Format.RGBA8_UNORM);
        using var msaa = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, samples: Samples);
        using var msaaView = msaa.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var array = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, mips: 2, layers: 3, size: 2 * Size);
        using var mip1Layer1 = array.CreateView(new TextureViewDesc { type = TextureView.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM, mipOffset = 1, mipNum = 1, layerOffset = 1, layerNum = 1, planes = PlaneBits.ALL });
        (ushort Mip, ushort Layer)[] all = [(0, 0), (1, 0), (0, 1), (1, 1), (0, 2), (1, 2)];
        using var background = new TextureUpload(device, array, 4, 0xEE, all);
        using var readback = new TextureReadback(device, array, 4, all);

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(msaa, Undefined, ColorAttachment), new NriTextureBarrier(array, Undefined, TextureCopyDestination)]);
            background.Record(cmd);
            cmd.Barrier(new NriTextureBarrier(array, TextureCopyDestination, ColorAttachment));
            scene.Draw(cmd, msaaView, Clear(ClearBytes), resolveDst: mip1Layer1);
            cmd.Barrier(new NriTextureBarrier(array, ColorAttachment, TextureCopySource));
            readback.Record(cmd);
        });

        var images = readback.Read();
        for (var s = 0; s < all.Length; s++)
        {
            if (all[s] == (1, 1))
            {
                var counts = CoveredSampleCounts(images[s], Samples, out var misfits);
                Assert.True(misfits.Count == 0, $"mip 1 layer 1, the view: {misfits.Count} pixels are no resolve of the triangle, first {string.Join("; ", misfits.Take(4))}");
                Assert.Contains(counts, k => k is > 0 and < Samples);
            }
            else
                Assert.True(images[s].All(b => b == 0xEE), $"mip {all[s].Mip} layer {all[s].Layer}, outside the view: {images[s].Count(b => b != 0xEE)} bytes changed");
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// An attachment resolve of a two-layer view resolves both layers: a two-layer 4x attachment
    /// cleared to one colour at BeginRendering, resolved at EndRendering into a two-layer texture
    /// whose layers held a background, reads back that colour in both layers. Before patch 0036
    /// NRI's D3D12 backend resolved only the view's first layer (EndRendering, fixed on NRI's main by
    /// 48d4a33 with the destination mip): layer 1 kept its background.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_attachment_resolve_of_a_layered_view_resolves_every_layer(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultisampleColor(device, Format.RGBA8_UNORM);
        RequireResolveOp(device, device.Desc.resolve.attachment.color, ResolveOp.AVERAGE, "resolve.attachment.color");

        using var msaa = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, layers: 2, samples: Samples);
        using var msaaView = msaa.CreateView(new TextureViewDesc { type = TextureView.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM, mipNum = 1, layerNum = 2, planes = PlaneBits.ALL });
        using var layered = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, layers: 2);
        using var layeredView = layered.CreateView(new TextureViewDesc { type = TextureView.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM, mipNum = 1, layerNum = 2, planes = PlaneBits.ALL });
        (ushort Mip, ushort Layer)[] both = [(0, 0), (0, 1)];
        using var background = new TextureUpload(device, layered, 4, 0xEE, both);
        using var readback = new TextureReadback(device, layered, 4, both);

        var red = new ClearValue();
        red.color.f = new Color32f { x = 1, w = 1 };
        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(msaa, Undefined, ColorAttachment), new NriTextureBarrier(layered, Undefined, TextureCopyDestination)]);
            background.Record(cmd);
            cmd.Barrier(new NriTextureBarrier(layered, TextureCopyDestination, ColorAttachment));
            cmd.BeginRendering([new NriAttachment(msaaView, red, LoadOp.CLEAR, StoreOp.STORE, ResolveOp.AVERAGE, layeredView)]);
            cmd.EndRendering();
            cmd.Barrier(new NriTextureBarrier(layered, ColorAttachment, TextureCopySource));
            readback.Record(cmd);
        });

        var images = readback.Read();
        for (var layer = 0; layer < 2; layer++)
        {
            var pixels = images[layer];
            var wrong = Enumerable.Range(0, pixels.Length / 4).Count(p => pixels[4 * p] != 255 || pixels[4 * p + 1] != 0 || pixels[4 * p + 2] != 0 || pixels[4 * p + 3] != 255);
            Assert.True(wrong == 0, $"layer {layer}: {wrong} of {pixels.Length / 4} pixels are not the resolved clear colour, the first ({pixels[0]},{pixels[1]},{pixels[2]},{pixels[3]})");
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdResolveTexture with a source and a destination region (features.regionResolve): a 6x4
    /// region at (5, 7) of layer 1 of a two-layer 4x texture lands at (3, 2) in mip 1, layer 1 of a
    /// two-mip, two-layer texture, and nothing else of the destination changes. Source layer 1 holds
    /// MsaaPattern.hlsl's position code in every sample, layer 0 one flat colour, so each resolved
    /// pixel names the source pixel it came from.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_region_resolve_lands_in_the_destination_mip_and_layer_and_nowhere_else(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultisampleColor(device, Format.RGBA8_UNORM);
        RequireResolveOp(device, device.Desc.resolve.command.color, ResolveOp.AVERAGE, "resolve.command.color");
        Assert.SkipWhen(device.Desc.features.regionResolve == 0, $"{backend}: features.regionResolve is false");

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
        using var pattern = CreatePipeline(device, layout, "MsaaPattern", Format.RGBA8_UNORM, Samples);
        using var msaa = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, samples: Samples, layers: 2);
        using var layer0 = msaa.CreateView(LayerView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM, 0));
        using var layer1 = msaa.CreateView(LayerView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM, 1));
        using var resolved = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.SHADER_RESOURCE, mips: 2, layers: 2);
        (ushort Mip, ushort Layer)[] subresources = [(0, 0), (1, 0), (0, 1), (1, 1)];
        using var background = new TextureUpload(device, resolved, 4, 0xEE, subresources);
        using var readback = new TextureReadback(device, resolved, 4, subresources);

        var source = new TextureRegionDesc { x = 5, y = 7, width = 6, height = 4, layerOffset = 1, planes = PlaneBits.COLOR };
        var destination = new TextureRegionDesc { x = 3, y = 2, mipOffset = 1, layerOffset = 1, planes = PlaneBits.COLOR };

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(msaa, Undefined, ColorAttachment), new NriTextureBarrier(resolved, Undefined, TextureCopyDestination)]);
            background.Record(cmd);
            cmd.BeginRendering([new NriAttachment(layer0, Clear([10, 20, 30, 255]), LoadOp.CLEAR, StoreOp.STORE)]);
            cmd.EndRendering();
            cmd.BeginRendering([new NriAttachment(layer1, Clear([0, 0, 0, 0]), LoadOp.CLEAR, StoreOp.STORE)]);
            SetViewportAndScissor(cmd, Size, Size);
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pattern);
            cmd.Draw(3);
            cmd.EndRendering();
            cmd.Barrier([], [], [new NriTextureBarrier(msaa, ColorAttachment, ResolveSource), new NriTextureBarrier(resolved, TextureCopyDestination, ResolveDestination)]);
            cmd.ResolveTexture(resolved, destination, msaa, source, ResolveOp.AVERAGE);
            cmd.Barrier(new NriTextureBarrier(resolved, ResolveDestination, TextureCopySource));
            readback.Record(cmd);
        });

        var images = readback.Read();
        var wrong = new List<string>();
        for (var s = 0; s < subresources.Length; s++)
        {
            var (mip, layer) = subresources[s];
            var width = Size >> mip;
            for (var y = 0; y < width; y++)
            for (var x = 0; x < width; x++)
            {
                var inRegion = mip == 1 && layer == 1 && x >= destination.x && x < destination.x + source.width && y >= destination.y && y < destination.y + source.height;
                byte[] expected = inRegion
                    ? [(byte)(4 * (source.x + x - destination.x)), (byte)(4 * (source.y + y - destination.y)), 128, 255]
                    : [0xEE, 0xEE, 0xEE, 0xEE];
                var i = (y * width + x) * 4;
                if (!images[s].AsSpan(i, 4).SequenceEqual(expected))
                    wrong.Add($"mip {mip} layer {layer} ({x},{y}) = ({string.Join(",", images[s][i..(i + 4)])}), expected ({string.Join(",", expected)})");
            }
        }
        Record($"{backend}: {wrong.Count} wrong pixel(s) in the four destination subresources");
        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(12)));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdResolveTexture with MIN, MAX and SAMPLE_ZERO, where the device reports the op in
    /// DeviceDesc::resolve.command for a format family (color: RGBA8_UNORM, colorInteger:
    /// RGBA8_UINT) whose format supports MULTISAMPLE_RESOLVE. Each sample holds the clear colour or
    /// the triangle's, so MIN gives their component-wise minimum on the partially covered pixels and
    /// MAX their maximum, colours neither of the two has; SAMPLE_ZERO gives one of the two, never a
    /// mix. Fully covered and uncovered pixels keep their colour under every op.
    /// </summary>
    [Theory]
    [MemberData(nameof(GpuBackendsAndResolveOps))]
    public void A_resolve_op_takes_the_minimum_maximum_or_sample_zero(Backend backend, ResolveOp op)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        ref readonly var resolve = ref device.Desc.resolve.command;
        var bit = (ResolveOpBits)(1 << (int)op);
        const FormatSupportBits needed = FormatSupportBits.COLOR_ATTACHMENT | FormatSupportBits.MULTISAMPLE_4X | FormatSupportBits.MULTISAMPLE_RESOLVE;
        Format? chosen = null;
        var reasons = new List<string>();
        foreach (var (field, reported, candidate) in new[] { ("color", resolve.color, Format.RGBA8_UNORM), ("colorInteger", resolve.colorInteger, Format.RGBA8_UINT) })
        {
            var missing = needed & ~device.GetFormatSupport(candidate);
            if ((reported & bit) == 0) reasons.Add($"resolve.command.{field} = {reported}");
            else if (missing != 0) reasons.Add($"resolve.command.{field} has {op}, but {candidate} lacks {missing}");
            else
            {
                chosen = candidate;
                break;
            }
        }
        Assert.SkipWhen(chosen is null, $"{backend}: no format family resolves with {op}: {string.Join("; ", reasons)}");
        var format = chosen!.Value;
        Record($"{backend}: {op} resolves {format}");

        using var scene = new TriangleScene(device, format);
        using var msaa = CreateTexture(device, format, TextureUsageBits.COLOR_ATTACHMENT, samples: Samples);
        using var msaaView = msaa.CreateView(TextureView.COLOR_ATTACHMENT, format);
        using var resolved = CreateTexture(device, format, TextureUsageBits.SHADER_RESOURCE);
        using var readback = new TextureReadback(device, resolved, 4, (0, 0));

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(msaa, Undefined, ColorAttachment), new NriTextureBarrier(resolved, Undefined, ResolveDestination)]);
            scene.Draw(cmd, msaaView, format == Format.RGBA8_UINT ? ClearUint(ClearBytes) : Clear(ClearBytes));
            cmd.Barrier(new NriTextureBarrier(msaa, ColorAttachment, ResolveSource));
            cmd.ResolveTexture(resolved, null, msaa, null, op);
            cmd.Barrier(new NriTextureBarrier(resolved, ResolveDestination, TextureCopySource));
            readback.Record(cmd);
        });

        var pixels = readback.Read()[0];
        var mixed = op switch
        {
            ResolveOp.MIN => ClearBytes.Zip(TriangleBytes, Math.Min).ToArray(),
            ResolveOp.MAX => ClearBytes.Zip(TriangleBytes, Math.Max).ToArray(),
            _ => null,
        };
        int clear = 0, triangle = 0, partial = 0;
        var wrong = new List<string>();
        for (var p = 0; p < Size * Size; p++)
        {
            var pixel = pixels.AsSpan(p * 4, 4);
            if (pixel.SequenceEqual(ClearBytes)) clear++;
            else if (pixel.SequenceEqual(TriangleBytes)) triangle++;
            else if (mixed is not null && pixel.SequenceEqual(mixed)) partial++;
            else wrong.Add($"({p % Size},{p / Size}) = ({string.Join(",", pixel.ToArray())})");
        }
        Record($"{backend}: {op}: {clear} clear, {triangle} triangle, {partial} {(mixed is null ? "" : $"({string.Join(",", mixed)}) ")}pixel(s), {wrong.Count} other");

        Assert.True(wrong.Count == 0, $"{op} resolved pixels that are neither the clear colour, the triangle's nor their {op} mix: {string.Join("; ", wrong.Take(8))}");
        Assert.True(clear > 0 && triangle > 0, $"{clear} clear and {triangle} triangle pixels");
        if (mixed is not null) Assert.True(partial > 0, $"no pixel resolved to the {op} of the two colours, so no partially covered pixel was resolved with {op}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdSetSampleLocations (tiers.sampleLocations): with all four sample locations at the pixel
    /// centre every sample of a pixel is covered or none is, so no resolved pixel is a mix of the
    /// clear and the triangle colours. The same pipeline with the standard 4x locations, set the same
    /// way in the same command buffer, gives partially covered pixels again.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Sample_locations_at_the_pixel_centre_leave_no_partially_covered_pixel(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireMultisampleColor(device, Format.RGBA8_UNORM);
        RequireResolveOp(device, device.Desc.resolve.command.color, ResolveOp.AVERAGE, "resolve.command.color");
        Assert.SkipWhen(device.Desc.tiers.sampleLocations == 0, $"{backend}: tiers.sampleLocations is 0");

        using var scene = new TriangleScene(device, Format.RGBA8_UNORM, sampleLocations: true);
        using var centred = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, samples: Samples);
        using var centredView = centred.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var standard = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, samples: Samples);
        using var standardView = standard.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var centredResolved = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.SHADER_RESOURCE);
        using var standardResolved = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.SHADER_RESOURCE);
        using var centredReadback = new TextureReadback(device, centredResolved, 4, (0, 0));
        using var standardReadback = new TextureReadback(device, standardResolved, 4, (0, 0));

        // In 1/16 pixel from the pixel centre, [-8, 7]. The standard 4x pattern of D3D and Vulkan.
        SampleLocation[] atCentre = [default, default, default, default];
        SampleLocation[] standardPattern = [new() { x = -2, y = -6 }, new() { x = 6, y = -2 }, new() { x = -6, y = 2 }, new() { x = 2, y = 6 }];

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [
                new NriTextureBarrier(centred, Undefined, ColorAttachment), new NriTextureBarrier(standard, Undefined, ColorAttachment),
                new NriTextureBarrier(centredResolved, Undefined, ResolveDestination), new NriTextureBarrier(standardResolved, Undefined, ResolveDestination),
            ]);
            scene.Draw(cmd, centredView, Clear(ClearBytes), before: c => c.SetSampleLocations(atCentre, Samples));
            scene.Draw(cmd, standardView, Clear(ClearBytes), before: c => c.SetSampleLocations(standardPattern, Samples));
            cmd.Barrier([], [], [new NriTextureBarrier(centred, ColorAttachment, ResolveSource), new NriTextureBarrier(standard, ColorAttachment, ResolveSource)]);
            cmd.ResolveTexture(centredResolved, null, centred, null, ResolveOp.AVERAGE);
            cmd.ResolveTexture(standardResolved, null, standard, null, ResolveOp.AVERAGE);
            cmd.Barrier([], [], [new NriTextureBarrier(centredResolved, ResolveDestination, TextureCopySource), new NriTextureBarrier(standardResolved, ResolveDestination, TextureCopySource)]);
            centredReadback.Record(cmd);
            standardReadback.Record(cmd);
        });

        var centredCounts = CoveredSampleCounts(centredReadback.Read()[0], Samples, out var centredMisfits);
        var standardCounts = CoveredSampleCounts(standardReadback.Read()[0], Samples, out var standardMisfits);
        Record($"{backend}: partially covered pixels with all locations at the centre: {centredCounts.Count(k => k is > 0 and < Samples)}, with the standard locations: {standardCounts.Count(k => k is > 0 and < Samples)}");

        Assert.Empty(centredMisfits);
        Assert.Empty(standardMisfits);
        Assert.DoesNotContain(centredCounts, k => k is > 0 and < Samples);
        Assert.Contains(centredCounts, k => k == Samples);
        Assert.Contains(standardCounts, k => k is > 0 and < Samples);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdSetShadingRate (tiers.shadingRate): MsaaPattern.hlsl writes each pixel's position, so
    /// per-pixel shading gives every pixel its own colour. The left half is drawn at 2x2, the right
    /// half at 1x1, with the same pipeline in one pass: on the left each aligned 2x2 block is one
    /// invocation's colour (that of a pixel of the block), on the right no two pixels of a block are
    /// equal.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_2x2_shading_rate_shades_each_2x2_block_once(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.tiers.shadingRate == 0, $"{backend}: tiers.shadingRate is 0");

        const ushort size = 64;
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
        using var pipeline = CreatePipeline(device, layout, "MsaaPattern", Format.RGBA8_UNORM, 1, shadingRate: true);
        using var target = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, size: size);
        using var view = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var readback = new TextureReadback(device, target, 4, (0, 0));

        Run(device, cmd =>
        {
            cmd.Barrier(new NriTextureBarrier(target, Undefined, ColorAttachment));
            cmd.BeginRendering([new NriAttachment(view, Clear([0, 0, 0, 0]), LoadOp.CLEAR, StoreOp.STORE)]);
            cmd.SetViewport(new Viewport { width = size, height = size, depthMax = 1 });
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetScissor(new Rect { x = 0, y = 0, width = size / 2, height = size });
            cmd.SetShadingRate(new ShadingRateDesc { shadingRate = ShadingRate.FRAGMENT_SIZE_2X2, primitiveCombiner = ShadingRateCombiner.KEEP, attachmentCombiner = ShadingRateCombiner.KEEP });
            cmd.Draw(3);
            cmd.SetScissor(new Rect { x = size / 2, y = 0, width = size / 2, height = size });
            cmd.SetShadingRate(new ShadingRateDesc { shadingRate = ShadingRate.FRAGMENT_SIZE_1X1, primitiveCombiner = ShadingRateCombiner.KEEP, attachmentCombiner = ShadingRateCombiner.KEEP });
            cmd.Draw(3);
            cmd.EndRendering();
            cmd.Barrier(new NriTextureBarrier(target, ColorAttachment, TextureCopySource));
            readback.Record(cmd);
        });

        var (coarseBlocks, fineBlocks, wrong) = ClassifyBlocks(readback.Read()[0], size, half: x => x < size / 2);
        Record($"{backend}: left half (2x2): {coarseBlocks.Uniform} of {coarseBlocks.Total} blocks uniform; right half (1x1): {fineBlocks.Uniform} of {fineBlocks.Total} uniform");

        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(8)));
        Assert.Equal(coarseBlocks.Total, coarseBlocks.Uniform);
        Assert.Equal(0, fineBlocks.Uniform);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A shading-rate attachment (RenderingDesc::shadingRate, tiers.shadingRate >= 2): one R8_UINT
    /// texel per shadingRateAttachmentTileSize square, NRI_SHADING_RATE(1, 1) = 2x2 in the left
    /// half's texels and 1x1 in the right half's, applied with attachmentCombiner REPLACE over a 1x1
    /// pipeline rate. One draw over the whole target: the left half comes out in uniform 2x2 blocks,
    /// the right half per pixel.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shading_rate_attachment_gives_two_halves_two_rates(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.tiers.shadingRate < 2, $"{backend}: tiers.shadingRate = {device.Desc.tiers.shadingRate}; a shading-rate attachment needs 2");
        var tile = device.Desc.other.shadingRateAttachmentTileSize;
        Assert.True(tile > 0, $"{backend}: tiers.shadingRate = 2 with other.shadingRateAttachmentTileSize = 0");

        // Four tiles across, so each half is two whole tile columns.
        var size = (ushort)(4 * tile);
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
        using var pipeline = CreatePipeline(device, layout, "MsaaPattern", Format.RGBA8_UNORM, 1, shadingRate: true);
        using var target = CreateTexture(device, Format.RGBA8_UNORM, TextureUsageBits.COLOR_ATTACHMENT, size: size);
        using var view = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var rates = CreateTexture(device, Format.R8_UINT, TextureUsageBits.SHADING_RATE_ATTACHMENT, size: 4);
        using var ratesView = rates.CreateView(TextureView.SHADING_RATE_ATTACHMENT, Format.R8_UINT);
        using var readback = new TextureReadback(device, target, 4, (0, 0));

        // NRI_SHADING_RATE(xLogSize, yLogSize) = (xLogSize << 2) | yLogSize (NRI.hlsl).
        const byte rate2x2 = (1 << 2) | 1, rate1x1 = 0;
        var texels = new byte[4 * 4];
        for (var i = 0; i < texels.Length; i++) texels[i] = i % 4 < 2 ? rate2x2 : rate1x1;
        using var rateUpload = new TextureUpload(device, rates, 1, texels);
        var shadingRateSource = new AccessLayoutStage { access = AccessBits.SHADING_RATE_ATTACHMENT, layout = Layout.SHADING_RATE_ATTACHMENT, stages = StageBits.SHADING_RATE_ATTACHMENT };

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(target, Undefined, ColorAttachment), new NriTextureBarrier(rates, Undefined, TextureCopyDestination)]);
            rateUpload.Record(cmd);
            cmd.Barrier(new NriTextureBarrier(rates, TextureCopyDestination, shadingRateSource));
            cmd.BeginRendering([new NriAttachment(view, Clear([0, 0, 0, 0]), LoadOp.CLEAR, StoreOp.STORE)], shadingRate: ratesView);
            SetViewportAndScissor(cmd, size, size);
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetShadingRate(new ShadingRateDesc { shadingRate = ShadingRate.FRAGMENT_SIZE_1X1, primitiveCombiner = ShadingRateCombiner.KEEP, attachmentCombiner = ShadingRateCombiner.REPLACE });
            cmd.Draw(3);
            cmd.EndRendering();
            cmd.Barrier(new NriTextureBarrier(target, ColorAttachment, TextureCopySource));
            readback.Record(cmd);
        });

        var (coarseBlocks, fineBlocks, wrong) = ClassifyBlocks(readback.Read()[0], size, half: x => x < size / 2);
        Record($"{backend}: tile {tile}, target {size}x{size}: left half (2x2 texels): {coarseBlocks.Uniform} of {coarseBlocks.Total} blocks uniform; right half (1x1 texels): {fineBlocks.Uniform} of {fineBlocks.Total} uniform");

        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(8)));
        Assert.Equal(coarseBlocks.Total, coarseBlocks.Uniform);
        Assert.Equal(0, fineBlocks.Uniform);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- checks

    /// <summary>
    /// For each pixel, the number k of covered samples for which it is (k * triangle + (n - k) *
    /// clear) / n within 1 in every channel, or -1 (listed in <paramref name="misfits"/>) if there is none.
    /// </summary>
    private static int[] CoveredSampleCounts(byte[] pixels, int n, out List<string> misfits)
    {
        misfits = [];
        var counts = new int[pixels.Length / 4];
        for (var p = 0; p < counts.Length; p++)
        {
            counts[p] = -1;
            for (var k = 0; k <= n && counts[p] < 0; k++)
            {
                var fits = true;
                for (var c = 0; c < 4 && fits; c++)
                {
                    var expected = (k * TriangleBytes[c] + (n - k) * ClearBytes[c]) / (double)n;
                    fits = Math.Abs(pixels[p * 4 + c] - expected) <= 1.0;
                }
                if (fits) counts[p] = k;
            }
            if (counts[p] < 0) misfits.Add($"({p % Size},{p / Size}) = ({string.Join(",", pixels[(p * 4)..(p * 4 + 4)])})");
        }
        return counts;
    }

    private readonly record struct BlockCount(int Total, int Uniform);

    /// <summary>
    /// Splits a square MsaaPattern.hlsl image into aligned 2x2 blocks and counts the uniform ones in
    /// each half. A uniform block must carry the position code of one of its own four pixels.
    /// </summary>
    private static (BlockCount Coarse, BlockCount Fine, List<string> Wrong) ClassifyBlocks(byte[] pixels, int size, Func<int, bool> half)
    {
        int coarseTotal = 0, coarseUniform = 0, fineTotal = 0, fineUniform = 0;
        var wrong = new List<string>();
        for (var by = 0; by < size; by += 2)
        for (var bx = 0; bx < size; bx += 2)
        {
            var first = pixels.AsSpan((by * size + bx) * 4, 4).ToArray();
            var uniform = true;
            for (var y = by; y < by + 2; y++)
            for (var x = bx; x < bx + 2; x++)
            {
                var i = (y * size + x) * 4;
                if (!pixels.AsSpan(i, 4).SequenceEqual(first)) uniform = false;
                if (pixels[i + 2] != 128 || pixels[i + 3] != 255)
                    wrong.Add($"({x},{y}) = ({string.Join(",", pixels[i..(i + 4)])}) is not a position code: not drawn");
            }
            // MsaaPattern.hlsl codes x and y modulo 64; blocks are aligned, so a block never wraps.
            if (uniform && (first[0] / 4 - bx % 64 is < 0 or > 1 || first[1] / 4 - by % 64 is < 0 or > 1))
                wrong.Add($"block at ({bx},{by}) is ({string.Join(",", first)}), the code of no pixel of the block");
            if (half(bx))
            {
                coarseTotal++;
                if (uniform) coarseUniform++;
            }
            else
            {
                fineTotal++;
                if (uniform) fineUniform++;
            }
        }
        return (new BlockCount(coarseTotal, coarseUniform), new BlockCount(fineTotal, fineUniform), wrong);
    }

    private static void AssertPixel(byte[] pixels, int x, int y, byte[] expected, string what)
    {
        var i = (y * Size + x) * 4;
        Assert.True(pixels.AsSpan(i, 4).SequenceEqual(expected), $"{what} ({x},{y}) is ({string.Join(",", pixels[i..(i + 4)])}), expected ({string.Join(",", expected)})");
    }

    // ---------------------------------------------------------------- requirements

    private static void RequireMultisampleColor(NriDevice device, Format format)
    {
        const FormatSupportBits needed = FormatSupportBits.COLOR_ATTACHMENT | FormatSupportBits.MULTISAMPLE_4X | FormatSupportBits.MULTISAMPLE_RESOLVE;
        var support = device.GetFormatSupport(format);
        Assert.SkipWhen((support & needed) != needed, $"{device.GraphicsApi}: {format} lacks {needed & ~support} (has {support})");
    }

    private static void RequireResolveOp(NriDevice device, ResolveOpBits reported, ResolveOp op, string field) =>
        Assert.SkipWhen((reported & (ResolveOpBits)(1 << (int)op)) == 0, $"{device.GraphicsApi}: {field} = {reported}, without {op}");

    // ---------------------------------------------------------------- resources

    private static ClearValue Clear(byte[] rgba)
    {
        var clear = new ClearValue();
        clear.color.f = new Color32f { x = rgba[0] / 255f, y = rgba[1] / 255f, z = rgba[2] / 255f, w = rgba[3] / 255f };
        return clear;
    }

    private static ClearValue ClearUint(byte[] rgba)
    {
        var clear = new ClearValue();
        clear.color.ui = new Color32ui { x = rgba[0], y = rgba[1], z = rgba[2], w = rgba[3] };
        return clear;
    }

    private static NriTexture CreateTexture(NriDevice device, Format format, TextureUsageBits usage, byte samples = 1, ushort mips = 1, ushort layers = 1, ushort size = Size) =>
        device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = usage,
            format = format,
            width = size,
            height = size,
            depth = 1,
            mipNum = mips,
            layerNum = layers,
            sampleNum = samples,
        });

    private static TextureViewDesc LayerView(TextureView type, Format format, ushort layer) => new()
    {
        type = type,
        format = format,
        mipOffset = 0,
        mipNum = 1,
        layerOffset = layer,
        layerNum = 1,
        sliceNum = NriApi.REMAINING,
        planes = PlaneBits.ALL,
    };

    private static void SetViewportAndScissor(NriCommandBuffer cmd, ushort width, ushort height)
    {
        cmd.SetViewport(new Viewport { x = 0, y = 0, width = width, height = height, depthMin = 0, depthMax = 1 });
        cmd.SetScissor(new Rect { x = 0, y = 0, width = width, height = height });
    }

    private static byte[] Shader(NriDevice device, string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name + (device.GraphicsApi == GraphicsAPI.VK ? ".spv" : ".dxil")));

    private static NriPipeline CreatePipeline(NriDevice device, NriPipelineLayout layout, string shader, Format format, byte samples, bool sampleLocations = false, bool shadingRate = false, string? pixelShader = null) =>
        device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            InputAssembly = new InputAssemblyDesc { topology = Topology.TRIANGLE_LIST },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE, shadingRate = (byte)(shadingRate ? 1 : 0) },
            Multisample = new MultisampleDesc { sampleMask = NriApi.ALL, sampleNum = samples, sampleLocations = (byte)(sampleLocations ? 1 : 0) },
            OutputMerger = new NriOutputMergerDesc { Colors = [new ColorAttachmentDesc { format = format, colorWriteMask = ColorWriteBits.RGBA }] },
            Shaders =
            [
                new NriShaderDesc(StageBits.VERTEX_SHADER, Shader(device, shader + ".vs"), "vsMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, Shader(device, (pixelShader ?? shader) + ".ps"), "psMain"),
            ],
        });

    /// <summary>MsaaTriangle.hlsl at 4x into a <see cref="Size"/>-square attachment of one format.</summary>
    private sealed class TriangleScene : IDisposable
    {
        private readonly NriPipelineLayout _layout;
        private readonly NriPipeline _pipeline;

        public TriangleScene(NriDevice device, Format format, bool sampleLocations = false)
        {
            _layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
            var pixelShader = format == Format.RGBA8_UINT ? "MsaaTriangleUint" : "MsaaTriangle";
            _pipeline = CreatePipeline(device, _layout, "MsaaTriangle", format, Samples, sampleLocations, pixelShader: pixelShader);
        }

        /// <summary>Clears <paramref name="target"/>, draws the triangle; <paramref name="before"/> runs inside the pass before the draw.</summary>
        public void Draw(NriCommandBuffer cmd, NriDescriptor target, ClearValue clear, NriDescriptor? resolveDst = null, Action<NriCommandBuffer>? before = null)
        {
            cmd.BeginRendering([new NriAttachment(target, clear, LoadOp.CLEAR, StoreOp.STORE, ResolveOp.AVERAGE, resolveDst)]);
            SetViewportAndScissor(cmd, Size, Size);
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, _layout);
            cmd.SetPipeline(_pipeline);
            before?.Invoke(cmd);
            cmd.Draw(3);
            cmd.EndRendering();
        }

        public void Dispose()
        {
            _pipeline.Dispose();
            _layout.Dispose();
        }
    }

    /// <summary>
    /// Fills subresources of a single-sample texture through one HOST_UPLOAD buffer, either every
    /// byte with one value or (one subresource) with given bytes. The texture must be in COPY_DESTINATION.
    /// </summary>
    private sealed class TextureUpload : IDisposable
    {
        private readonly NriBuffer _buffer;
        private readonly NriTexture _texture;
        private readonly List<(TextureRegionDesc Region, TextureDataLayoutDesc Layout)> _parts = [];

        public TextureUpload(NriDevice device, NriTexture texture, int texelBytes, byte fill, params (ushort Mip, ushort Layer)[] subresources)
            : this(device, texture, texelBytes, subresources, (_, span) => span.Fill(fill)) { }

        public TextureUpload(NriDevice device, NriTexture texture, int texelBytes, byte[] bytes)
            : this(device, texture, texelBytes, [(0, 0)], (_, span) => bytes.CopyTo(span)) { }

        private TextureUpload(NriDevice device, NriTexture texture, int texelBytes, (ushort Mip, ushort Layer)[] subresources, Action<int, Span<byte>> fill)
        {
            _texture = texture;
            var layouts = Layouts(device, texture, texelBytes, subresources, out var size);
            _buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = size });
            var mapped = _buffer.Map();
            for (var s = 0; s < layouts.Count; s++)
            {
                var (region, layout, rowBytes, rows) = layouts[s];
                var tight = new byte[rowBytes * rows];
                fill(s, tight);
                for (var y = 0; y < rows; y++)
                    tight.AsSpan(y * rowBytes, rowBytes).CopyTo(mapped[(int)(layout.offset + (ulong)(y * layout.rowPitch))..]);
                _parts.Add((region, layout));
            }
            _buffer.Unmap();
        }

        public void Record(NriCommandBuffer cmd)
        {
            foreach (var (region, layout) in _parts) cmd.UploadBufferToTexture(_texture, region, _buffer, layout);
        }

        public void Dispose() => _buffer.Dispose();
    }

    /// <summary>Reads subresources of a single-sample texture back through one HOST_READBACK buffer, tightly packed.</summary>
    private sealed class TextureReadback : IDisposable
    {
        private readonly NriBuffer _buffer;
        private readonly NriTexture _texture;
        private readonly List<(TextureRegionDesc Region, TextureDataLayoutDesc Layout, int RowBytes, int Rows)> _parts;

        public TextureReadback(NriDevice device, NriTexture texture, int texelBytes, params (ushort Mip, ushort Layer)[] subresources)
        {
            _texture = texture;
            _parts = Layouts(device, texture, texelBytes, subresources, out var size);
            _buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = size });
        }

        /// <summary>The texture must be in COPY_SOURCE.</summary>
        public void Record(NriCommandBuffer cmd)
        {
            foreach (var (region, layout, _, _) in _parts) cmd.ReadbackTextureToBuffer(_buffer, layout, _texture, region);
        }

        public byte[][] Read()
        {
            var mapped = _buffer.Map();
            var result = new byte[_parts.Count][];
            for (var s = 0; s < _parts.Count; s++)
            {
                var (_, layout, rowBytes, rows) = _parts[s];
                result[s] = new byte[rowBytes * rows];
                for (var y = 0; y < rows; y++)
                    mapped.Slice((int)(layout.offset + (ulong)(y * layout.rowPitch)), rowBytes).CopyTo(result[s].AsSpan(y * rowBytes));
            }
            _buffer.Unmap();
            return result;
        }

        public void Dispose() => _buffer.Dispose();
    }

    /// <summary>Whole-subresource regions and their buffer layouts at the device's row and slice alignment.</summary>
    private static List<(TextureRegionDesc Region, TextureDataLayoutDesc Layout, int RowBytes, int Rows)> Layouts(
        NriDevice device, NriTexture texture, int texelBytes, (ushort Mip, ushort Layer)[] subresources, out ulong size)
    {
        ref readonly var alignment = ref device.Desc.memoryAlignment;
        ref readonly var desc = ref texture.Desc;
        var parts = new List<(TextureRegionDesc, TextureDataLayoutDesc, int, int)>();
        ulong offset = 0;
        foreach (var (mip, layer) in subresources)
        {
            var width = Math.Max(1, desc.width >> mip);
            var height = Math.Max(1, desc.height >> mip);
            var rowBytes = width * texelBytes;
            var rowPitch = (uint)Align((ulong)rowBytes, alignment.uploadBufferTextureRow);
            offset = Align(offset, alignment.uploadBufferTextureSlice);
            var slicePitch = (uint)Align((ulong)rowPitch * (ulong)height, alignment.uploadBufferTextureSlice);
            parts.Add((
                new TextureRegionDesc { width = (ushort)width, height = (ushort)height, depth = 1, mipOffset = mip, layerOffset = layer, planes = PlaneBits.COLOR },
                new TextureDataLayoutDesc { offset = offset, rowPitch = rowPitch, slicePitch = slicePitch },
                rowBytes, height));
            offset += slicePitch;
        }
        size = Math.Max(offset, 4);
        return parts;
    }
}
