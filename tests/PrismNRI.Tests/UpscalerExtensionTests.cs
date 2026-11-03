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

    /// <summary>
    /// <c>IsRuntimeInstalled</c> answers whether every vendor file <c>GetRuntimeFiles</c> names for the
    /// type and graphics API is beside the application, for every type, without asking NRI: true
    /// for the types built into NRI (NIS, MetalFX) whether or not this build compiled them in, and for
    /// the vendor types exactly when their files are there (no PrismNRI package ships them, so a
    /// bare test directory answers false). The device overload answers for the device's API. The
    /// file names are the ones NRI and its SDKs load by name on Windows: FSR's loader and upscaler
    /// on D3D12, FSR's Vulkan library, XeSS, and NGX's DLSS super resolution and ray reconstruction.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void IsRuntimeInstalled_follows_the_vendor_files_beside_the_application_for_every_type(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var ext = device.Upscaler;
        var linux = OperatingSystem.IsLinux();

        var expectedFiles = new Dictionary<UpscalerType, string[]>
        {
            [UpscalerType.NIS] = [],
            [UpscalerType.FSR] = backend.Api == GraphicsAPI.D3D12 ? ["amd_fidelityfx_loader_dx12.dll", "amd_fidelityfx_upscaler_dx12.dll"] : ["amd_fidelityfx_vk.dll"],
            [UpscalerType.XESS] = ["libxess.dll"],
            [UpscalerType.DLSR] = [linux ? "libnvidia-ngx-dlss.so.*" : "nvngx_dlss.dll"],
            [UpscalerType.DLRR] = [linux ? "libnvidia-ngx-dlssd.so.*" : "nvngx_dlssd.dll"],
            [UpscalerType.METALFX_SPATIAL] = [],
            [UpscalerType.METALFX_TEMPORAL] = [],
            [UpscalerType.METALFX_DENOISER] = [],
        };
        Assert.Equal(Enum.GetValues<UpscalerType>().Where(t => t != UpscalerType.MAX_NUM), expectedFiles.Keys);

        foreach (var (type, files) in expectedFiles)
        {
            Assert.Equal(files, NriUpscalerExtension.GetRuntimeFiles(type, backend.Api));
            var present = files.Where(f => f.Contains('*')
                ? Directory.EnumerateFiles(AppContext.BaseDirectory, f).Any()
                : File.Exists(Path.Combine(AppContext.BaseDirectory, f))).ToArray();
            var installed = NriUpscalerExtension.IsRuntimeInstalled(type, backend.Api);
            GpuWork.Record($"{backend}: {type}: compiled in {ext.IsUpscalerSupported(type)}, runtime files [{string.Join(", ", files)}], present [{string.Join(", ", present)}], installed {installed}");
            Assert.Equal(present.Length == files.Length, installed);
            Assert.Equal(installed, ext.IsRuntimeInstalled(type));
        }
        Assert.True(NriUpscalerExtension.IsRuntimeInstalled(UpscalerType.NIS, backend.Api), "NIS is built into NRI and needs no vendor file");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// <c>CreateUpscaler</c> for a type this NRI build lacks (<c>IsUpscalerSupported</c> false: every
    /// type of the Base variant on VK and D3D12) asks no runtime file and lets NRI answer: its
    /// validation reports "CreateUpscaler: 'type' is not supported" and returns UNSUPPORTED, so
    /// NriUnsupportedException with <c>Call</c> "CreateUpscaler" and no object created. For a type
    /// NRI claims whose vendor file is missing, the binding refuses before NRI is called (XeSS would
    /// otherwise fault in its delay-load stub, CLAUDE.md: Upscalers and vendor runtimes), naming the
    /// file, and NRI says nothing. On the Base payload only NONE claims a vendor type (it claims
    /// every type), so NONE is where that refusal is reachable; it is the binding's own check, NONE
    /// is not asked anything. Types claimed with their runtime present are not created here.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void CreateUpscaler_answers_UNSUPPORTED_for_a_missing_type_or_runtime_before_NRI_creates_anything(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var ext = device.Upscaler;

        var tried = 0;
        foreach (var type in Enum.GetValues<UpscalerType>().Where(t => t != UpscalerType.MAX_NUM))
        {
            var supported = ext.IsUpscalerSupported(type);
            if (supported && ext.IsRuntimeInstalled(type)) continue;
            var live = device.LiveObjects;
            var start = messages.Count;
            var e = Assert.Throws<NriUnsupportedException>(() => ext.CreateUpscaler(new NriUpscalerDesc
            {
                UpscaleResolution = new Dim2_t { w = 64, h = 64 },
                Type = type,
                Mode = UpscalerMode.NATIVE,
                OutputFormat = Format.RGBA8_UNORM,
            }));
            List<NriMessage> provoked;
            lock (messages) provoked = messages.Skip(start).ToList();
            GpuWork.Record($"{backend}: {type} (compiled in {supported}): {e.Message}; {string.Join(" | ", provoked)}");
            Assert.Equal("CreateUpscaler", e.Call);
            Assert.Equal(Result.UNSUPPORTED, e.Result);
            Assert.Equal(live, device.LiveObjects);
            if (supported)
            {
                Assert.Contains($"needs the vendor runtime '{NriUpscalerExtension.GetRuntimeFiles(type, backend.Api).First(f => !File.Exists(Path.Combine(AppContext.BaseDirectory, f)))}'", e.Message, StringComparison.Ordinal);
                Assert.Empty(provoked);
            }
            else
            {
                Assert.DoesNotContain("vendor runtime", e.Message, StringComparison.Ordinal);
                var refusal = Assert.Single(provoked);
                Assert.True(refusal.IsError, $"expected NRI validation's ERROR, got {refusal}");
                Assert.Contains("CreateUpscaler: 'type' is not supported", refusal.Text, StringComparison.Ordinal);
            }
            lock (messages) messages.RemoveRange(start, messages.Count - start); // the refusal is the answer, not a failure
            tried++;
        }
        Assert.SkipWhen(tried == 0, $"{backend}: every upscaler type is compiled in and has its runtime beside the tests; nothing to refuse");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// <c>DispatchUpscale</c> allocates nothing on the managed heap ("No heap allocation", its doc)
    /// for each union arm it writes: NIS settings, the upscaler guides (METALFX_TEMPORAL), and the
    /// denoiser guides with the DLRR settings (METALFX_DENOISER), every guide resource filled.
    /// Measured as ExtensionAllocationTests measures: on NONE, which claims every upscaler type and
    /// does nothing per call, after a warm-up, over 300 dispatches of each. The vendor types (FSR,
    /// XeSS, DLSS) cannot be created without their runtime files, which no package ships.
    /// </summary>
    [Fact]
    public void DispatchUpscale_writes_each_union_arm_without_allocating()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.NONE));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);
        var ext = device.Upscaler;
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var cmd = allocator.CreateCommandBuffer();
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc { type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM, width = 4, height = 4 });
        using var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        var resource = new NriUpscalerResource(texture, view);

        UpscalerType[] types = [UpscalerType.NIS, UpscalerType.METALFX_TEMPORAL, UpscalerType.METALFX_DENOISER];
        var upscalers = types.Select(t => ext.CreateUpscaler(new NriUpscalerDesc { UpscaleResolution = new Dim2_t { w = 8, h = 8 }, Type = t })).ToArray();
        try
        {
            Assert.Equal(types, upscalers.Select(u => u.Type));
            var dispatch = new NriDispatchUpscaleDesc
            {
                Input = resource,
                Output = resource,
                Guides = new NriDispatchUpscaleGuides
                {
                    Upscaler = new NriUpscalerGuides { Mv = resource, Depth = resource, Exposure = resource, Reactive = resource },
                    Denoiser = new NriDenoiserGuides
                    {
                        Mv = resource, Depth = resource, NormalRoughness = resource, DiffuseAlbedo = resource, SpecularAlbedo = resource,
                        SpecularMvOrHitT = resource, Exposure = resource, Reactive = resource, PreTransparency = resource, Sss = resource,
                    },
                },
                Settings = new NriDispatchUpscaleSettings { Nis = new NISSettings { sharpness = 0.5f }, Fsr = new FSRSettings(), Dlrr = new DLRRSettings() },
                CurrentResolution = new Dim2_t { w = 4, h = 4 },
                PreExposure = 1,
            };

            void Frame()
            {
                cmd.Begin();
                foreach (var upscaler in upscalers) ext.DispatchUpscale(cmd, upscaler, dispatch);
                cmd.End();
            }

            Frame(); // warm-up
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 300; i++) Frame();
            var delta = GC.GetAllocatedBytesForCurrentThread() - before;
            GpuWork.Record($"NONE: 300 x {upscalers.Length} upscale dispatches: {delta} managed bytes");
            Assert.Equal(0, delta);
        }
        finally
        {
            foreach (var upscaler in upscalers) upscaler.Dispose();
        }
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
