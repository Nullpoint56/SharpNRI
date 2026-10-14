// The Upscaler extension: acquisition everywhere, which upscaler types each backend reports, and on
// the GPU backends a real NIS upscale (NIS is built into NRI): a constant-colour 32x32 input
// upscaled to 64x64, read back, and checked to still be that colour.

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class UpscalerExtensionTests
{
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void The_extension_is_available_and_reports_its_types(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        Assert.NotNull(device.TryUpscaler);
        var supported = Enum.GetValues<UpscalerType>().Where(t => t != UpscalerType.MAX_NUM && device.Upscaler.IsUpscalerSupported(t)).ToList();
        GpuWork.Record($"{backend}: supported upscalers: {string.Join(", ", supported)}");

        // What is supported is what the native variant compiled in (CLAUDE.md: Native packages): the Base
        // variant has no upscaler SDK and reports none on the GPU backends; the Upscalers variant
        // reports NIS, FSR and XeSS; NONE claims everything.
        if (backend.Api == GraphicsAPI.NONE) Assert.Contains(UpscalerType.NIS, supported);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>Skips when the staged native variant has no NIS, which is the Base variant.</summary>
    private static void RequireNis(NriDevice device) =>
        Assert.SkipUnless(device.TryUpscaler?.IsUpscalerSupported(UpscalerType.NIS) == true,
            OperatingSystem.IsMacOS()
                ? "NIS is not compiled into the staged libNRI.dylib (Base variant, the only one built for osx-arm64)"
                : "NIS is not compiled into the staged NRI.dll (Base variant); run tools/build-native.ps1 -Variant Upscalers and copy its NRI.dll beside the tests");

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void NIS_upscales_a_constant_image_to_the_same_constant(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireNis(device);
        var ext = device.Upscaler;

        const ushort In = 32, Out = 64;
        byte[] colour = [200, 100, 50, 255];

        using var upscaler = ext.CreateUpscaler(new NriUpscalerDesc
        {
            UpscaleResolution = new Dim2_t { w = Out, h = Out },
            Type = UpscalerType.NIS,
            OutputFormat = Format.RGBA8_UNORM,
        });
        var props = ext.GetUpscalerProps(upscaler);
        GpuWork.Record($"{backend}: NIS props scaling {props.scalingFactor}, render {props.renderResolution.w}x{props.renderResolution.h}, upscale {props.upscaleResolution.w}x{props.upscaleResolution.h}");
        Assert.Equal(Out, props.upscaleResolution.w);

        using var input = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = In, height = In, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var output = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE_STORAGE, format = Format.RGBA8_UNORM,
            width = Out, height = Out, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var inputView = input.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        using var outputView = output.CreateView(TextureView.STORAGE_TEXTURE, Format.RGBA8_UNORM);

        ref readonly var desc = ref device.Desc;
        var inRow = (uint)GpuWork.Align(In * 4, desc.memoryAlignment.uploadBufferTextureRow);
        var inSlice = (uint)GpuWork.Align(inRow * In, desc.memoryAlignment.uploadBufferTextureSlice);
        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = inSlice });
        var mapped = upload.Map();
        for (var y = 0; y < In; y++)
            for (var x = 0; x < In; x++)
                colour.CopyTo(mapped.Slice((int)(y * inRow) + x * 4, 4));
        upload.Unmap();

        var outRow = (uint)GpuWork.Align(Out * 4, desc.memoryAlignment.uploadBufferTextureRow);
        var outSlice = (uint)GpuWork.Align(outRow * Out, desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = outSlice });

        var shaderResource = new AccessLayoutStage { access = AccessBits.SHADER_RESOURCE, layout = Layout.SHADER_RESOURCE, stages = StageBits.COMPUTE_SHADER };
        var storage = new AccessLayoutStage { access = AccessBits.SHADER_RESOURCE_STORAGE, layout = Layout.SHADER_RESOURCE_STORAGE, stages = StageBits.COMPUTE_SHADER };

        GpuWork.Run(device, cb =>
        {
            cb.Barrier([], [], [new NriTextureBarrier(input, GpuWork.Undefined, GpuWork.TextureCopyDestination), new NriTextureBarrier(output, GpuWork.Undefined, storage)]);
            cb.UploadBufferToTexture(input, new TextureRegionDesc { width = In, height = In, depth = 1, planes = PlaneBits.COLOR }, upload,
                new TextureDataLayoutDesc { rowPitch = inRow, slicePitch = inSlice });
            cb.Barrier(new NriTextureBarrier(input, GpuWork.TextureCopyDestination, shaderResource));

            ext.DispatchUpscale(cb, upscaler, new NriDispatchUpscaleDesc
            {
                Input = new NriUpscalerResource(input, inputView),
                Output = new NriUpscalerResource(output, outputView),
                Settings = new NriDispatchUpscaleSettings { Nis = new NISSettings { sharpness = 0 } },
                CurrentResolution = new Dim2_t { w = In, h = In },
            });

            cb.Barrier(new NriTextureBarrier(output, storage, GpuWork.TextureCopySource));
            cb.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = outRow, slicePitch = outSlice }, output,
                new TextureRegionDesc { width = Out, height = Out, depth = 1, planes = PlaneBits.COLOR });
        });

        var pixels = readback.Map().ToArray();
        readback.Unmap();
        foreach (var (x, y) in new[] { (0, 0), (Out / 2, Out / 2), (Out - 1, Out - 1), (10, 50) })
        {
            var i = (int)(y * outRow) + x * 4;
            for (var c = 0; c < 3; c++)
                Assert.True(Math.Abs(pixels[i + c] - colour[c]) <= 3, $"({x},{y}) channel {c} is {pixels[i + c]}, expected about {colour[c]}");
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
