// Every upscaler NRI can build, dispatched for real on every backend that offers it: NIS, FSR, XeSS,
// DLSS super resolution (DLSR) and ray reconstruction (DLRR). These run only on a local payload
// with the SDKs compiled in and the vendor runtimes beside the tests
// (-p:SharpNRIDevPayload=upscalers or upscalers-dlss, tools/upscalers.ps1); no package ships those
// runtimes (AGENTS.md: Upscalers and vendor runtimes), so on the Base payload, as in CI, every test
// skips naming what is missing.
//
// NRI checks a failed vendor dispatch (ffxDispatch, xessD3D12Execute, NGX's EvaluateFeature) only
// with NRI_CHECK, which a release NRI.dll compiles out (UpscalerInterface.hpp), so the pixels are the
// only oracle: every dispatch here is read back and compared with what the scene must give.
//
// The scenes are drawn on the CPU and uploaded each frame: a constant colour (which every upscaler
// must keep, whatever its mode, jitter or history), a vertical edge moving at a known speed with
// matching motion vectors (which must land where the edge is, at output resolution), a colour
// change with and without RESET_HISTORY, and a static edge with and without sharpening. Figures
// (render resolutions, deviations, edge errors, GPU times) are written as test output and, when
// SHARPNRI_UPSCALER_FIGURES names a file, as JSON lines that tools/upscalers.ps1 turns into
// docs/upscalers.md.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SharpNRI.Native;
using SharpNRI.Testing;
using Xunit;

namespace SharpNRI.Tests;

public sealed class UpscalerDispatchTests
{
    private static readonly UpscalerType[] Types = [UpscalerType.NIS, UpscalerType.FSR, UpscalerType.XESS, UpscalerType.DLSR, UpscalerType.DLRR];
    private static readonly UpscalerType[] Temporal = [UpscalerType.FSR, UpscalerType.XESS, UpscalerType.DLSR, UpscalerType.DLRR];
    private static readonly UpscalerMode[] Modes = Enum.GetValues<UpscalerMode>().Where(m => m != UpscalerMode.MAX_NUM).ToArray();

    public static TheoryData<Backend, UpscalerType> BackendsAndTypes() => Combine(Types);
    public static TheoryData<Backend, UpscalerType> BackendsAndTemporalTypes() => Combine(Temporal);
    public static TheoryData<Backend, UpscalerType> BackendsAndSharpeningTypes() => Combine([UpscalerType.NIS, UpscalerType.FSR]);

    public static TheoryData<Backend, UpscalerType, UpscalerMode> BackendsTypesAndModes()
    {
        var data = new TheoryData<Backend, UpscalerType, UpscalerMode>();
        foreach (var backend in NriTestDevice.GpuBackends.Select(a => new Backend(a)))
            foreach (var type in Types)
                foreach (var mode in Modes)
                    data.Add(backend, type, mode);
        return data;
    }

    private static TheoryData<Backend, UpscalerType> Combine(UpscalerType[] types)
    {
        var data = new TheoryData<Backend, UpscalerType>();
        foreach (var backend in NriTestDevice.GpuBackends.Select(a => new Backend(a)))
            foreach (var type in types)
                data.Add(backend, type);
        return data;
    }

    // ================================================================================= the tests

    /// <summary>
    /// Every mode creates, and <c>GetUpscalerProps</c> reports what <c>NRIUpscaler.h</c> documents for it:
    /// the mode's scaling factor (1.0, 1.3, 1.5, 1.7, 2.0, 3.0), the render resolution
    /// <c>upscale / factor</c> rounded, <c>mipBias = -log2(factor) - 1</c>, <c>jitterPhaseNum =
    /// ceil(8 factor^2)</c>, the output resolution unchanged, and a minimum render resolution no
    /// larger than the render resolution. Created for a 4K output; the creation time is a figure.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndTypes))]
    public void Every_mode_creates_and_reports_its_documented_props(Backend backend, UpscalerType type)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        var ext = device.Upscaler;
        var factors = new Dictionary<UpscalerMode, float>
        {
            [UpscalerMode.NATIVE] = 1.0f, [UpscalerMode.ULTRA_QUALITY] = 1.3f, [UpscalerMode.QUALITY] = 1.5f,
            [UpscalerMode.BALANCED] = 1.7f, [UpscalerMode.PERFORMANCE] = 2.0f, [UpscalerMode.ULTRA_PERFORMANCE] = 3.0f,
        };
        var output = new Dim2_t { w = 3840, h = 2160 };
        foreach (var mode in Modes)
        {
            var clock = Stopwatch.StartNew();
            using var upscaler = ext.CreateUpscaler(new NriUpscalerDesc { UpscaleResolution = output, Type = type, Mode = mode, OutputFormat = Format.RGBA16_SFLOAT });
            var createMs = clock.Elapsed.TotalMilliseconds;
            var props = ext.GetUpscalerProps(upscaler);
            Figures.Add(backend, type, "props", new
            {
                mode = mode.ToString(), props.scalingFactor, props.mipBias, render = $"{props.renderResolution.w}x{props.renderResolution.h}",
                renderMin = $"{props.renderResolutionMin.w}x{props.renderResolutionMin.h}", props.jitterPhaseNum, createMs = Math.Round(createMs, 1),
            });

            var factor = factors[mode];
            Assert.Equal(factor, props.scalingFactor);
            Assert.Equal(output, props.upscaleResolution);
            Assert.Equal((ushort)(output.w / factor + 0.5f), props.renderResolution.w);
            Assert.Equal((ushort)(output.h / factor + 0.5f), props.renderResolution.h);
            Assert.Equal(-MathF.Log2(factor) - 1, props.mipBias, 5);
            Assert.Equal((byte)MathF.Ceiling(8 * factor * factor), props.jitterPhaseNum);
            Assert.True(props.renderResolutionMin.w <= props.renderResolution.w && props.renderResolutionMin.h <= props.renderResolution.h,
                $"{mode}: minimum render resolution {props.renderResolutionMin.w}x{props.renderResolutionMin.h} above {props.renderResolution.w}x{props.renderResolution.h}");
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A constant colour stays that colour, in every mode, over 8 jittered frames (Halton 2,3) with
    /// zero motion: the first with RESET_HISTORY, all at the mode's render resolution, and where the
    /// type allows dynamic resolution (a minimum below the render resolution) 8 more at the minimum.
    /// Every output pixel is compared; the largest deviation is a figure. Linear LDR input, automatic
    /// exposure, RGBA16_SFLOAT throughout, output 512x288.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsTypesAndModes))]
    public void A_constant_image_upscales_to_the_same_constant_in_every_mode(Backend backend, UpscalerType type, UpscalerMode mode)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        using var rig = new Rig(device, type, mode, new Dim2_t { w = 512, h = 288 }, UpscalerBits.NONE);
        float[] colour = [0.25f, 0.5f, 0.75f];

        var deviation = 0f;
        for (var frame = 0; frame < 8; frame++)
            rig.Frame(rig.Render, (_, _) => colour, Halton(frame), frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE);
        deviation = MaxDeviation(rig.ReadOutput(), colour);
        var report = $"render {rig.Render.w}x{rig.Render.h}: max deviation {deviation:F4}";

        float? dynamicDeviation = null;
        if (rig.Props.renderResolutionMin.w < rig.Render.w && rig.Props.renderResolutionMin.w > 0)
        {
            var start = messages.Count;
            for (var frame = 0; frame < 8; frame++)
                rig.Frame(rig.Props.renderResolutionMin, (_, _) => colour, Halton(frame), frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE);
            dynamicDeviation = MaxDeviation(rig.ReadOutput(), colour);
            report += $"; at the minimum {rig.Props.renderResolutionMin.w}x{rig.Props.renderResolutionMin.h}: {dynamicDeviation:F4}";

            // DLSS ray reconstruction on D3D12, input below its creation size (measured, NGX 310.9.1):
            // NGX's own transition barriers on its internal resources name a state the resource is not
            // in, "[527] ... Before state (0x400: D3D12_RESOURCE_STATE_COPY_DEST) of resource
            // (...:'nv.ngx.dlssd.resource') ... does not match with the state (0x40:
            // D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE)", twice per test, while the output stays
            // right. NGX's internal resources, not NRI's or the test's: allowed by ID and resource name
            // only, and the test fails when they stop, naming this allowance (AGENTS.md: Upscalers and
            // vendor runtimes).
            if (backend.Api == GraphicsAPI.D3D12 && type == UpscalerType.DLRR)
            {
                int allowed;
                lock (messages) allowed = messages.Skip(start).Count(IsNgxDlssdStateError);
                lock (messages) messages.RemoveAll(IsNgxDlssdStateError);
                report += $"; NGX's [527] on 'nv.ngx.dlssd.resource' {allowed} times";
                Assert.True(allowed > 0, $"{backend} {type} {mode}: NGX's [527] barrier errors on 'nv.ngx.dlssd.resource' at a reduced input size are gone: remove their allowance from this test and AGENTS.md");
            }
        }
        Figures.Add(backend, type, "constant", new { mode = mode.ToString(), render = $"{rig.Render.w}x{rig.Render.h}", deviation, dynamicDeviation });
        GpuWork.Record($"{backend} {type} {mode}: {report}");

        Assert.True(deviation <= ConstantTolerance, $"{backend} {type} {mode}: {report}, more than {ConstantTolerance}");
        Assert.True(dynamicDeviation is null || dynamicDeviation <= ConstantTolerance, $"{backend} {type} {mode}: {report}, more than {ConstantTolerance}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The constant colour again with the HDR inputs: an HDR colour (4, 2, 1) with <c>HDR</c>, a 1x1
    /// exposure texture with <c>USE_EXPOSURE</c>, and a pre-exposure of 2 already applied to the input
    /// (so the input holds 8, 4, 2 and the output must too). QUALITY mode.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndTemporalTypes))]
    public void An_HDR_constant_with_an_exposure_texture_and_pre_exposure_stays_constant(Backend backend, UpscalerType type)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        using var rig = new Rig(device, type, UpscalerMode.QUALITY, new Dim2_t { w = 512, h = 288 }, UpscalerBits.HDR | UpscalerBits.USE_EXPOSURE);
        const float preExposure = 2;
        float[] colour = [4 * preExposure, 2 * preExposure, 1 * preExposure];

        for (var frame = 0; frame < 8; frame++)
            rig.Frame(rig.Render, (_, _) => colour, Halton(frame), frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE, preExposure: preExposure);
        var output = rig.ReadOutput();
        var deviation = MaxRelativeDeviation(output, colour);
        Figures.Add(backend, type, "hdr", new { relativeDeviation = deviation });
        GpuWork.Record($"{backend} {type}: HDR constant ({string.Join(", ", colour)}), pre-exposure {preExposure}: max relative deviation {deviation:F4}");

        Assert.True(deviation <= ConstantTolerance, $"{backend} {type}: HDR constant deviates by {deviation:F4} relative, more than {ConstantTolerance}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    public static TheoryData<Backend, UpscalerType, Format> BackendsTemporalTypesAndMaskFormats()
    {
        var data = new TheoryData<Backend, UpscalerType, Format>();
        foreach (var backend in NriTestDevice.GpuBackends.Select(a => new Backend(a)))
            foreach (var type in Temporal)
                foreach (var format in new[] { Format.R8_UNORM, Format.R16_SFLOAT })
                    data.Add(backend, type, format);
        return data;
    }

    /// <summary>
    /// A reactive mask (<c>USE_REACTIVE</c>, all 0: no bias towards the input) in two formats leaves
    /// a constant image constant. NRIUpscaler.h names no format for it.
    /// DLSS ray reconstruction on D3D12 cannot take an R8_UNORM mask (measured, NGX 310.9.1): it
    /// creates its own view of the mask with the resource's format, which is R8_TYPELESS because NRI
    /// creates every D3D12 texture TYPELESS, the debug layer reports "[28]
    /// ID3D12Device::CreateShaderResourceView: The Format (0x3c, R8_TYPELESS) is invalid", and the
    /// device is removed, which then fails every later D3D12CreateDevice in the process (0x887A0001).
    /// That case skips quoting it; R16_SFLOAT works there, and DLSS super resolution takes R8_UNORM
    /// (AGENTS.md: Upscalers and vendor runtimes).
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsTemporalTypesAndMaskFormats))]
    public void A_reactive_mask_leaves_a_constant_image_constant(Backend backend, UpscalerType type, Format format)
    {
        Assert.SkipWhen(backend.Api == GraphicsAPI.D3D12 && type == UpscalerType.DLRR && format == Format.R8_UNORM,
            "D3D12 DLRR views an R8 reactive mask as R8_TYPELESS (NGX 310.9.1): debug error [28] at CreateShaderResourceView, then device removal (DXGI_ERROR_INVALID_CALL) that fails every later D3D12 device in the process");
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        using var rig = new Rig(device, type, UpscalerMode.QUALITY, new Dim2_t { w = 512, h = 288 }, UpscalerBits.USE_REACTIVE, reactiveFormat: format);
        float[] colour = [0.25f, 0.5f, 0.75f];

        for (var frame = 0; frame < 8; frame++)
            rig.Frame(rig.Render, (_, _) => colour, Halton(frame), frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE);
        var deviation = MaxDeviation(rig.ReadOutput(), colour);
        Figures.Add(backend, type, "reactive", new { format = format.ToString(), deviation });
        GpuWork.Record($"{backend} {type}: reactive mask {format}: max deviation {deviation:F4}");

        Assert.True(deviation <= ConstantTolerance, $"{backend} {type}: with a {format} reactive mask the constant deviates by {deviation:F4}, more than {ConstantTolerance}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A vertical edge, dark left and bright right, moving right by 2 render pixels per frame for 16
    /// frames with motion vectors that say so (-2, 0 pixels: towards the previous position): in the
    /// last output, in every row, the edge must lie within 2 output pixels of where the scene puts it
    /// (its render position times the scaling factor), and both sides at least 8 pixels away from it
    /// must keep their levels. No jitter, so a sign convention cannot shift the edge. QUALITY mode
    /// (NIS, which has no history, sees the same frames).
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndTypes))]
    public void A_moving_edge_lands_where_the_scene_puts_it(Backend backend, UpscalerType type)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        using var rig = new Rig(device, type, UpscalerMode.QUALITY, new Dim2_t { w = 512, h = 288 }, UpscalerBits.NONE);
        float[] dark = [0.1f, 0.1f, 0.1f], bright = [0.8f, 0.8f, 0.8f];
        const int Frames = 16, Speed = 2;
        var edge = 0;

        for (var frame = 0; frame < Frames; frame++)
        {
            edge = rig.Render.w / 4 + frame * Speed;
            var at = edge;
            rig.Frame(rig.Render, (x, _) => x < at ? dark : bright, default, frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE, mv: new Float2_t { x = -Speed, y = 0 });
        }
        var output = rig.ReadOutput();
        var scale = (float)rig.Output.w / rig.Render.w;
        var expected = edge * scale;
        var (worstError, worstSide) = (0f, 0f);
        for (var y = 0; y < rig.Output.h; y++)
        {
            var found = -1;
            for (var x = 0; x < rig.Output.w && found < 0; x++)
                if (output.Luma(x, y) >= (dark[0] + bright[0]) / 2) found = x;
            worstError = MathF.Max(worstError, found < 0 ? float.PositiveInfinity : MathF.Abs(found - expected));
            for (var x = 0; x < rig.Output.w; x++)
            {
                if (MathF.Abs(x + 0.5f - expected) < 8) continue;
                var level = x + 0.5f < expected ? dark[0] : bright[0];
                worstSide = MathF.Max(worstSide, MathF.Abs(output.Luma(x, y) - level));
            }
        }
        Figures.Add(backend, type, "edge", new { expectedX = expected, edgeError = worstError, sideDeviation = worstSide });
        GpuWork.Record($"{backend} {type}: edge expected at output x {expected:F1}; worst row off by {worstError:F1} px; sides deviate by up to {worstSide:F4}");

        Assert.True(worstError <= 2, $"{backend} {type}: the edge is {worstError:F1} output pixels from x {expected:F1} in some row");
        Assert.True(worstSide <= EdgeSideTolerance, $"{backend} {type}: the flat sides deviate by {worstSide:F4} from their levels, more than {EdgeSideTolerance}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// History: 8 frames of one colour, then a different colour for one frame with RESET_HISTORY,
    /// which must be exactly the new colour everywhere (within the constant tolerance). The same
    /// change without RESET_HISTORY is measured and reported (how much of the old colour one frame
    /// keeps), not asserted: each upscaler's history rejection is its own.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndTemporalTypes))]
    public void RESET_HISTORY_makes_the_next_frame_forget_the_previous_image(Backend backend, UpscalerType type)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        using var rig = new Rig(device, type, UpscalerMode.QUALITY, new Dim2_t { w = 512, h = 288 }, UpscalerBits.NONE);
        float[] before = [0.8f, 0.2f, 0.2f], after = [0.2f, 0.2f, 0.8f];

        float Change(DispatchUpscaleBits flags)
        {
            for (var frame = 0; frame < 8; frame++)
                rig.Frame(rig.Render, (_, _) => before, Halton(frame), frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE);
            rig.Frame(rig.Render, (_, _) => after, Halton(8), flags);
            return MaxDeviation(rig.ReadOutput(), after);
        }

        var withReset = Change(DispatchUpscaleBits.RESET_HISTORY);
        var withoutReset = Change(DispatchUpscaleBits.NONE);
        Figures.Add(backend, type, "reset", new { withReset, withoutReset });
        GpuWork.Record($"{backend} {type}: one frame after a colour change: max deviation {withReset:F4} with RESET_HISTORY, {withoutReset:F4} without");

        Assert.True(withReset <= ConstantTolerance, $"{backend} {type}: with RESET_HISTORY the new colour deviates by {withReset:F4}, more than {ConstantTolerance}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Sharpening (<c>NISSettings.sharpness</c>, <c>FSRSettings.sharpness</c>, the two types with a
    /// setting) reaches the upscaler: fine low-contrast stripes come out with a larger amplitude at
    /// sharpness 1 than at 0. Both amplitudes are figures.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndSharpeningTypes))]
    public void Sharpness_raises_the_contrast_of_fine_detail(Backend backend, UpscalerType type)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        using var rig = new Rig(device, type, UpscalerMode.QUALITY, new Dim2_t { w = 512, h = 288 }, UpscalerBits.NONE);
        // Low-contrast stripes, 3 render pixels each: NIS sharpens low-contrast detail and holds back
        // on strong edges (a 0.2 / 0.6 edge measured the same overshoot, 0.046, at both settings)
        float[] dark = [0.45f, 0.45f, 0.45f], bright = [0.55f, 0.55f, 0.55f];

        float Amplitude(float sharpness)
        {
            for (var frame = 0; frame < 8; frame++)
                rig.Frame(rig.Render, (x, _) => x / 3 % 2 == 0 ? dark : bright, default, frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE, sharpness: sharpness);
            var output = rig.ReadOutput();
            float min = float.MaxValue, max = float.MinValue;
            for (var y = rig.Output.h / 4; y < rig.Output.h * 3 / 4; y++)
                for (var x = rig.Output.w / 4; x < rig.Output.w * 3 / 4; x++)
                {
                    min = MathF.Min(min, output.Luma(x, y));
                    max = MathF.Max(max, output.Luma(x, y));
                }
            return max - min;
        }

        var soft = Amplitude(0);
        var sharp = Amplitude(1);
        Figures.Add(backend, type, "sharpness", new { amplitude0 = soft, amplitude1 = sharp, input = bright[0] - dark[0] });
        GpuWork.Record($"{backend} {type}: stripes of amplitude {bright[0] - dark[0]:F2} come out at {soft:F4} with sharpness 0, {sharp:F4} with sharpness 1");

        Assert.True(sharp > soft + 0.005f, $"{backend} {type}: amplitude {sharp:F4} at sharpness 1, {soft:F4} at sharpness 0: the setting made no difference");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// DLSS creation work recorded into the caller's command buffer (<c>UpscalerDesc::commandBuffer</c>,
    /// "caller submits it"): created inside a recording command buffer that is then submitted, the
    /// upscaler dispatches the constant image correctly. Without one NRI submits and waits itself,
    /// which the other tests cover.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndTypes))]
    public void DLSS_creation_recorded_into_the_callers_command_buffer_works(Backend backend, UpscalerType type)
    {
        Assert.SkipUnless(type is UpscalerType.DLSR or UpscalerType.DLRR, $"{type} takes no creation command buffer (NRIUpscaler.h: DLSR/DLRR only)");
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();
        commandBuffer.Begin();
        using var rig = new Rig(device, type, UpscalerMode.QUALITY, new Dim2_t { w = 512, h = 288 }, UpscalerBits.NONE, commandBuffer);
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 1);
        GpuWork.Wait(fence, 1);

        float[] colour = [0.25f, 0.5f, 0.75f];
        for (var frame = 0; frame < 4; frame++)
            rig.Frame(rig.Render, (_, _) => colour, Halton(frame), frame == 0 ? DispatchUpscaleBits.RESET_HISTORY : DispatchUpscaleBits.NONE);
        var deviation = MaxDeviation(rig.ReadOutput(), colour);
        GpuWork.Record($"{backend} {type}: created in the caller's command buffer: max deviation {deviation:F4}");
        Assert.True(deviation <= ConstantTolerance, $"{backend} {type}: deviation {deviation:F4}, more than {ConstantTolerance}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NRI validation refuses a dispatch whose <c>currentResolution</c> is above the render resolution
    /// ("'currentResolution' is above the maximum render resolution") or below the minimum, and
    /// a camera jitter outside [-0.5, 0.5], each with one ERROR and nothing recorded.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndTypes))]
    public void Validation_refuses_a_resolution_or_jitter_out_of_range(Backend backend, UpscalerType type)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages);
        using var rig = new Rig(device, type, UpscalerMode.QUALITY, new Dim2_t { w = 512, h = 288 }, UpscalerBits.NONE);
        var tooLarge = new Dim2_t { w = (ushort)(rig.Render.w + 1), h = rig.Render.h };
        var min = rig.Props.renderResolutionMin;
        List<(string What, Dim2_t Resolution, Float2_t Jitter, string Text)> cases =
        [
            ("above the render resolution", tooLarge, default, "'currentResolution' is above the maximum render resolution"),
            ("jitter x 0.6", rig.Render, new Float2_t { x = 0.6f }, "'cameraJitter.x' is out of range"),
        ];
        if (min.w > 0) cases.Add(("below the minimum", new Dim2_t { w = (ushort)(min.w - 1), h = min.h }, default, "'currentResolution' is below the minimum render resolution"));

        foreach (var (what, resolution, jitter, text) in cases)
        {
            var start = messages.Count;
            rig.RecordOnly(resolution, jitter);
            List<NriMessage> provoked;
            lock (messages) provoked = messages.Skip(start).ToList();
            GpuWork.Record($"{backend} {type}: {what}: {string.Join(" | ", provoked)}");
            var refusal = Assert.Single(provoked);
            Assert.True(refusal.IsError);
            Assert.Contains(text, refusal.Text, StringComparison.Ordinal);
            lock (messages) messages.RemoveRange(start, messages.Count - start);
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// GPU time of one dispatch to a 3840x2160 output in every mode, between two timestamps: the median
    /// of 32 dispatches after 8 warm-up ones, on a device without NRI or API validation. Asserted only
    /// to be ordered and above 1 microsecond (a dispatch that failed inside the vendor SDK records
    /// nothing, and NRI does not report it in a release build); the times are the figure.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsAndTypes))]
    public void Dispatch_GPU_time_at_a_4K_output_in_every_mode(Backend backend, UpscalerType type)
    {
        var messages = new List<NriMessage>();
        using var device = Open(backend, type, messages, validated: false);
        Assert.SkipWhen(device.Desc.features.timestamp == 0, $"{backend}: features.timestamp is false");
        var frequency = device.Desc.other.timestampFrequencyHz;
        var summary = new List<string>();
        foreach (var mode in Modes)
        {
            using var rig = new Rig(device, type, mode, new Dim2_t { w = 3840, h = 2160 }, UpscalerBits.NONE);
            float[] colour = [0.25f, 0.5f, 0.75f];
            rig.Frame(rig.Render, (_, _) => colour, default, DispatchUpscaleBits.RESET_HISTORY);
            var ticks = rig.TimeDispatches(8, 32);
            var ms = ticks.Select(t => t * 1000.0 / frequency).Order().ToArray();
            var median = ms[ms.Length / 2];
            Figures.Add(backend, type, "timing", new { mode = mode.ToString(), render = $"{rig.Render.w}x{rig.Render.h}", medianMs = Math.Round(median, 3), minMs = Math.Round(ms[0], 3), maxMs = Math.Round(ms[^1], 3) });
            summary.Add($"{mode} {rig.Render.w}x{rig.Render.h} {median:F3} ms");
            Assert.True(ms[0] > 0.001, $"{backend} {type} {mode}: a dispatch took {ms[0] * 1000:F2} us: nothing ran");
        }
        GpuWork.Record($"{backend} {type}: median GPU time per dispatch to 3840x2160: {string.Join("; ", summary)}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ================================================================================= tolerances

    /// <summary>
    /// The largest deviation of any output channel from a constant input. Half precision holds 0.75
    /// to 0.0005; the rest is each upscaler's own arithmetic, measured in docs/upscalers.md.
    /// </summary>
    private const float ConstantTolerance = 0.02f;

    /// <summary>Flat regions of the moving edge, at least 8 output pixels away from it.</summary>
    private const float EdgeSideTolerance = 0.05f;

    // ================================================================================= helpers

    /// <summary>
    /// A validated device of <paramref name="backend"/> on which <paramref name="type"/> is compiled in,
    /// offered and has its runtime; skips naming what is missing otherwise.
    /// </summary>
    private static NriDevice Open(Backend backend, UpscalerType type, List<NriMessage> messages, bool validated = true)
    {
        TestDevice.RequireAvailable(backend);
        var device = TestDevice.Create(backend.Api, messages, o =>
        {
            o.EnableNriValidation = validated;
            o.EnableGraphicsApiValidation = validated;
        });
        try
        {
            var ext = device.Upscaler;
            Assert.SkipUnless(ext.IsUpscalerSupported(type),
                $"{backend}: IsUpscalerSupported({type}) is false: " + type switch
                {
                    UpscalerType.XESS when backend.Api != GraphicsAPI.D3D12 => "NRI offers XeSS on D3D12 only",
                    UpscalerType.DLSR or UpscalerType.DLRR when device.Desc.adapterDesc.vendor != Vendor.NVIDIA || device.Desc.tiers.rayTracing == 0 => "NRI offers DLSS only on an NVIDIA adapter with ray tracing",
                    UpscalerType.DLSR or UpscalerType.DLRR => "NGX is not compiled into the staged NRI.dll (build tools/build-native.ps1 -WithDlss and test with -p:SharpNRIDevPayload=upscalers-dlss)",
                    _ => "the SDK is not compiled into the staged NRI.dll (Base variant; build tools/build-native.ps1 -Variant Upscalers and test with -p:SharpNRIDevPayload=upscalers)",
                });
            Assert.SkipUnless(ext.IsRuntimeInstalled(type),
                $"{backend}: {type} needs {string.Join(", ", NriUpscalerExtension.GetRuntimeFiles(type, backend.Api))} beside the tests (-p:SharpNRIDevPayload copies them from the native build tree)");
            return device;
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    private static bool IsNgxDlssdStateError(NriMessage m) =>
        m.IsError && m.Text.Contains("[527]", StringComparison.Ordinal) && m.Text.Contains("'nv.ngx.dlssd.resource'", StringComparison.Ordinal);

    private static Float2_t Halton(int frame) => new() { x = Halton(frame + 1, 2) - 0.5f, y = Halton(frame + 1, 3) - 0.5f };

    private static float Halton(int index, int radix)
    {
        float result = 0, fraction = 1;
        for (var i = index; i > 0; i /= radix)
        {
            fraction /= radix;
            result += fraction * (i % radix);
        }
        return result;
    }

    private static float MaxDeviation(Image image, float[] colour)
    {
        var worst = 0f;
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                for (var c = 0; c < 3; c++)
                    worst = MathF.Max(worst, MathF.Abs(image[x, y, c] - colour[c]));
        return worst;
    }

    private static float MaxRelativeDeviation(Image image, float[] colour)
    {
        var worst = 0f;
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                for (var c = 0; c < 3; c++)
                    worst = MathF.Max(worst, MathF.Abs(image[x, y, c] - colour[c]) / colour[c]);
        return worst;
    }

    /// <summary>An RGBA16_SFLOAT image read back.</summary>
    private sealed class Image(int width, int height, Half[] texels)
    {
        public int Width => width;
        public int Height => height;
        public float this[int x, int y, int c] => (float)texels[(y * width + x) * 4 + c];
        public float Luma(int x, int y) => (this[x, y, 0] + this[x, y, 1] + this[x, y, 2]) / 3;
    }

    /// <summary>The figures sink: test output always, JSON lines when SHARPNRI_UPSCALER_FIGURES names a file.</summary>
    private static class Figures
    {
        private static readonly object s_lock = new();

        public static void Add(Backend backend, UpscalerType type, string kind, object values)
        {
            var path = Environment.GetEnvironmentVariable("SHARPNRI_UPSCALER_FIGURES");
            if (string.IsNullOrEmpty(path)) return;
            var line = JsonSerializer.Serialize(new { backend = backend.ToString(), type = type.ToString(), kind, values });
            lock (s_lock) File.AppendAllText(path, line + "\n");
        }
    }

    /// <summary>
    /// One upscaler with its textures: colour input, depth (R32_SFLOAT, constant 0.5), motion vectors
    /// (RG16_SFLOAT), a 1x1 exposure (R32_SFLOAT, 1) and a reactive mask (R8_UNORM, 0) when the flags
    /// ask, and for DLRR the denoiser guides (normal + roughness facing the camera, grey diffuse
    /// albedo, 0.04 specular albedo, hit distance 1) with an identity view and an orthographic clip.
    /// Each frame uploads the input and the motion vectors, dispatches, and waits.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        private readonly NriDevice _device;
        private readonly NriUpscalerExtension _ext;
        private readonly NriUpscaler _upscaler;
        private readonly List<IDisposable> _owned = [];
        private readonly Dictionary<NriTexture, AccessLayoutStage> _state = [];
        private readonly NriTexture _input, _output, _mv;
        private readonly NriBuffer _inputUpload, _mvUpload, _readback;
        private readonly (uint Row, uint Slice) _inputPitch, _mvPitch, _outputPitch;
        private readonly NriDispatchUpscaleDesc _dispatch;

        public UpscalerProps Props { get; }
        public Dim2_t Render => Props.renderResolution;
        public Dim2_t Output => Props.upscaleResolution;

        private static readonly AccessLayoutStage ShaderResource = new() { access = AccessBits.SHADER_RESOURCE, layout = Layout.SHADER_RESOURCE, stages = StageBits.COMPUTE_SHADER };
        private static readonly AccessLayoutStage Storage = new() { access = AccessBits.SHADER_RESOURCE_STORAGE, layout = Layout.SHADER_RESOURCE_STORAGE, stages = StageBits.COMPUTE_SHADER };

        public Rig(NriDevice device, UpscalerType type, UpscalerMode mode, Dim2_t output, UpscalerBits flags, NriCommandBuffer? creation = null, Format reactiveFormat = Format.R8_UNORM)
        {
            _device = device;
            _ext = device.Upscaler;
            // A throw part-way must release what was made: a D3D12 device left with children cannot be
            // disposed, and the next D3D12CreateDevice in the process then fails (AGENTS.md: Lifetime)
            try
            {
            _upscaler = Own(_ext.CreateUpscaler(new NriUpscalerDesc { UpscaleResolution = output, Type = type, Mode = mode, Flags = flags, OutputFormat = Format.RGBA16_SFLOAT, CommandBuffer = creation }));
            Props = _ext.GetUpscalerProps(_upscaler);
            var render = Render;

            (_input, var inputView) = Texture(render, Format.RGBA16_SFLOAT, storage: false);
            (_output, var outputView) = Texture(output, Format.RGBA16_SFLOAT, storage: true);
            (_mv, var mvView) = Texture(render, Format.RG16_SFLOAT, storage: false);
            _inputPitch = Pitch(render, 8);
            _mvPitch = Pitch(render, 4);
            _outputPitch = Pitch(output, 8);
            _inputUpload = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = _inputPitch.Slice }));
            _mvUpload = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = _mvPitch.Slice }));
            _readback = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = _outputPitch.Slice }));

            var statics = new List<(NriTexture Texture, Dim2_t Size, int Bytes, Action<Span<byte>> Fill)>();
            NriUpscalerResource Static(Dim2_t size, Format format, int bytes, Action<Span<byte>> fill)
            {
                var (texture, view) = Texture(size, format, storage: false);
                statics.Add((texture, size, bytes, fill));
                return new NriUpscalerResource(texture, view);
            }
            static Action<Span<byte>> Halves(params float[] value) => span =>
            {
                var halves = MemoryMarshal.Cast<byte, Half>(span);
                for (var i = 0; i < halves.Length; i++) halves[i] = (Half)value[i % value.Length];
            };
            static Action<Span<byte>> Floats(float value) => span => MemoryMarshal.Cast<byte, float>(span).Fill(value);

            var depth = Static(render, Format.R32_SFLOAT, 4, Floats(0.5f));
            var exposure = (flags & UpscalerBits.USE_EXPOSURE) != 0 ? Static(new Dim2_t { w = 1, h = 1 }, Format.R32_SFLOAT, 4, Floats(1)) : default;
            var reactive = (flags & UpscalerBits.USE_REACTIVE) != 0 ? Static(render, reactiveFormat, reactiveFormat == Format.R8_UNORM ? 1 : 2, span => span.Clear()) : default;
            var mvResource = new NriUpscalerResource(_mv, mvView);

            var guides = new NriDispatchUpscaleGuides();
            var settings = new NriDispatchUpscaleSettings();
            if (type == UpscalerType.DLRR)
            {
                guides = new NriDispatchUpscaleGuides
                {
                    Denoiser = new NriDenoiserGuides
                    {
                        Mv = mvResource, Depth = depth, Exposure = exposure, Reactive = reactive,
                        NormalRoughness = Static(render, Format.RGBA16_SFLOAT, 8, Halves(0, 0, 1, 0.5f)),
                        DiffuseAlbedo = Static(render, Format.RGBA16_SFLOAT, 8, Halves(0.5f, 0.5f, 0.5f, 1)),
                        SpecularAlbedo = Static(render, Format.RGBA16_SFLOAT, 8, Halves(0.04f, 0.04f, 0.04f, 1)),
                        SpecularMvOrHitT = Static(render, Format.R16_SFLOAT, 2, Halves(1)),
                    },
                };
                var dlrr = new DLRRSettings();
                for (var i = 0; i < 16; i += 5) { dlrr.worldToViewMatrix[i] = 1; dlrr.viewToClipMatrix[i] = 1; } // identity; orthographic: {A; B} = {0; 1}
                settings = new NriDispatchUpscaleSettings { Dlrr = dlrr };
            }
            else if (type != UpscalerType.NIS)
            {
                guides = new NriDispatchUpscaleGuides { Upscaler = new NriUpscalerGuides { Mv = mvResource, Depth = depth, Exposure = exposure, Reactive = reactive } };
                if (type == UpscalerType.FSR)
                    settings = new NriDispatchUpscaleSettings { Fsr = new FSRSettings { zNear = 0.1f, zFar = 1000, verticalFov = MathF.PI / 3, frameTime = 16.6f, viewSpaceToMetersFactor = 1 } };
            }
            _dispatch = new NriDispatchUpscaleDesc
            {
                Input = new NriUpscalerResource(_input, inputView),
                Output = new NriUpscalerResource(_output, outputView),
                Guides = guides,
                Settings = settings,
                MvScale = new Float2_t { x = 1, y = 1 },
            };

            // The guides that never change, uploaded once
            var uploads = statics.Select(s =>
            {
                var pitch = Pitch(s.Size, s.Bytes);
                var buffer = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = pitch.Slice }));
                var mapped = buffer.Map();
                for (var y = 0; y < s.Size.h; y++) s.Fill(mapped.Slice((int)(y * pitch.Row), s.Size.w * s.Bytes));
                buffer.Unmap();
                return (s.Texture, s.Size, Buffer: buffer, Pitch: pitch);
            }).ToList();
            GpuWork.Run(device, cb =>
            {
                foreach (var u in uploads)
                {
                    To(cb, u.Texture, GpuWork.TextureCopyDestination);
                    cb.UploadBufferToTexture(u.Texture, Region(u.Size), u.Buffer, new TextureDataLayoutDesc { rowPitch = u.Pitch.Row, slicePitch = u.Pitch.Slice });
                    To(cb, u.Texture, ShaderResource);
                }
            });
            }
            catch
            {
                Release();
                throw;
            }
        }

        /// <summary>One frame: <paramref name="colour"/> per render pixel and a uniform motion vector, uploaded, dispatched, waited for.</summary>
        public void Frame(Dim2_t current, Func<int, int, float[]> colour, Float2_t jitter, DispatchUpscaleBits flags, Float2_t mv = default, float preExposure = 0, float sharpness = 0)
        {
            var mapped = _inputUpload.Map();
            for (var y = 0; y < Render.h; y++)
            {
                var row = MemoryMarshal.Cast<byte, Half>(mapped.Slice((int)(y * _inputPitch.Row), Render.w * 8));
                for (var x = 0; x < Render.w; x++)
                {
                    var c = colour(x, y);
                    row[x * 4] = (Half)c[0]; row[x * 4 + 1] = (Half)c[1]; row[x * 4 + 2] = (Half)c[2]; row[x * 4 + 3] = (Half)1;
                }
            }
            _inputUpload.Unmap();
            mapped = _mvUpload.Map();
            for (var y = 0; y < Render.h; y++)
            {
                var row = MemoryMarshal.Cast<byte, Half>(mapped.Slice((int)(y * _mvPitch.Row), Render.w * 4));
                for (var x = 0; x < Render.w; x++) { row[x * 2] = (Half)mv.x; row[x * 2 + 1] = (Half)mv.y; }
            }
            _mvUpload.Unmap();

            var dispatch = _dispatch with
            {
                CurrentResolution = current,
                CameraJitter = jitter,
                PreExposure = preExposure,
                Flags = flags,
                Settings = _dispatch.Settings with { Nis = new NISSettings { sharpness = sharpness }, Fsr = _dispatch.Settings.Fsr with { sharpness = sharpness } },
            };
            GpuWork.Run(_device, cb =>
            {
                To(cb, _input, GpuWork.TextureCopyDestination);
                To(cb, _mv, GpuWork.TextureCopyDestination);
                cb.UploadBufferToTexture(_input, Region(Render), _inputUpload, new TextureDataLayoutDesc { rowPitch = _inputPitch.Row, slicePitch = _inputPitch.Slice });
                cb.UploadBufferToTexture(_mv, Region(Render), _mvUpload, new TextureDataLayoutDesc { rowPitch = _mvPitch.Row, slicePitch = _mvPitch.Slice });
                To(cb, _input, ShaderResource);
                To(cb, _mv, ShaderResource);
                To(cb, _output, Storage);
                _ext.DispatchUpscale(cb, _upscaler, dispatch);
            });
        }

        /// <summary>Records a dispatch with these values and never submits it (for the validation refusals).</summary>
        public void RecordOnly(Dim2_t current, Float2_t jitter)
        {
            var queue = _device.GetQueue(QueueType.GRAPHICS);
            using var allocator = queue.CreateCommandAllocator();
            using var cb = allocator.CreateCommandBuffer();
            cb.Begin();
            _ext.DispatchUpscale(cb, _upscaler, _dispatch with { CurrentResolution = current, CameraJitter = jitter });
            cb.End();
        }

        /// <summary>GPU ticks of <paramref name="timed"/> dispatches after <paramref name="warmUp"/>, each between two timestamps.</summary>
        public ulong[] TimeDispatches(int warmUp, int timed)
        {
            var count = (uint)(warmUp + timed);
            using var pool = _device.CreateQueryPool(QueryType.TIMESTAMP, 2 * count);
            using var readback = _device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 2 * count * pool.QuerySize });
            var dispatch = _dispatch with { CurrentResolution = Render };
            GpuWork.Run(_device, cb =>
            {
                cb.ResetQueries(pool, 0, 2 * count);
                for (uint i = 0; i < count; i++)
                {
                    cb.Barrier(new NriTextureBarrier(_output, Storage, Storage));
                    cb.EndQuery(pool, 2 * i);
                    _ext.DispatchUpscale(cb, _upscaler, dispatch);
                    cb.EndQuery(pool, 2 * i + 1);
                }
                cb.CopyQueries(pool, 0, 2 * count, readback, 0);
            });
            var ticks = MemoryMarshal.Cast<byte, ulong>(readback.Map()).ToArray();
            readback.Unmap();
            var result = new ulong[timed];
            for (var i = 0; i < timed; i++)
            {
                var (begin, end) = (ticks[2 * (warmUp + i)], ticks[2 * (warmUp + i) + 1]);
                Assert.True(end >= begin, $"timestamps out of order: {begin} then {end}");
                result[i] = end - begin;
            }
            return result;
        }

        public Image ReadOutput()
        {
            GpuWork.Run(_device, cb =>
            {
                To(cb, _output, GpuWork.TextureCopySource);
                cb.ReadbackTextureToBuffer(_readback, new TextureDataLayoutDesc { rowPitch = _outputPitch.Row, slicePitch = _outputPitch.Slice }, _output, Region(Output));
            });
            var mapped = _readback.Map();
            var texels = new Half[Output.w * Output.h * 4];
            for (var y = 0; y < Output.h; y++)
                MemoryMarshal.Cast<byte, Half>(mapped.Slice((int)(y * _outputPitch.Row), Output.w * 8)).CopyTo(texels.AsSpan(y * Output.w * 4));
            _readback.Unmap();
            return new Image(Output.w, Output.h, texels);
        }

        private void To(NriCommandBuffer cb, NriTexture texture, AccessLayoutStage target)
        {
            var before = _state.GetValueOrDefault(texture, GpuWork.Undefined);
            cb.Barrier(new NriTextureBarrier(texture, before, target));
            _state[texture] = target;
        }

        private (NriTexture, NriDescriptor) Texture(Dim2_t size, Format format, bool storage)
        {
            var texture = Own(_device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
            {
                type = TextureType.TEXTURE_2D, usage = storage ? TextureUsageBits.SHADER_RESOURCE_STORAGE : TextureUsageBits.SHADER_RESOURCE, format = format,
                width = size.w, height = size.h, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            }));
            var view = Own(texture.CreateView(storage ? TextureView.STORAGE_TEXTURE : TextureView.TEXTURE, format));
            return (texture, view);
        }

        private (uint Row, uint Slice) Pitch(Dim2_t size, int bytesPerTexel)
        {
            ref readonly var desc = ref _device.Desc;
            var row = (uint)GpuWork.Align((ulong)(size.w * bytesPerTexel), desc.memoryAlignment.uploadBufferTextureRow);
            return (row, (uint)GpuWork.Align(row * (ulong)size.h, desc.memoryAlignment.uploadBufferTextureSlice));
        }

        private static TextureRegionDesc Region(Dim2_t size) => new() { width = size.w, height = size.h, depth = 1, planes = PlaneBits.COLOR };

        private T Own<T>(T disposable) where T : IDisposable
        {
            _owned.Add(disposable);
            return disposable;
        }

        public void Dispose() => Release();

        private void Release()
        {
            _device.WaitIdle();
            // In reverse order of creation: views before their textures, the upscaler last
            for (var i = _owned.Count - 1; i >= 0; i--) _owned[i].Dispose();
            _owned.Clear();
        }
    }
}
