// The device-independent nri* exports through PrismNRI.Nri: format conversion both ways for every
// format in the generated enum, FormatProps against the enum, the API names, pipeline layout
// fitting against hand-made limits, and the thread-global annotations and live-object report
// beside a device that keeps working.

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class FlatExportTests
{
    private static IEnumerable<Format> AllFormats() => Enum.GetValues<Format>().Where(f => f is not Format.UNKNOWN and not Format.MAX_NUM);

    private static bool IsBlockCompressed(Format f) => Nri.GetFormatProps(f).isCompressed != 0;

    /// <summary>
    /// NRI → DXGI → NRI and NRI → Vulkan → NRI for every format, with the exceptions measured on
    /// NRI v190 named: DXGI has no ETC2 or ASTC; NRI's Vulkan-to-NRI table ends before the
    /// block-compressed VkFormats; NRI's DXGI table maps R32G32_SINT (18) to RGB32_SINT. A fixed
    /// table in a later NRI fails here, which is the point: the guide quotes these.
    /// </summary>
    [Fact]
    public void Formats_round_trip_through_DXGI_and_Vulkan_except_where_NRI_has_no_entry()
    {
        // Spot values from dxgiformat.h and vulkan_core.h.
        Assert.Equal(28u, Nri.ConvertNRIFormatToDXGI(Format.RGBA8_UNORM));
        Assert.Equal(37u, Nri.ConvertNRIFormatToVK(Format.RGBA8_UNORM));
        Assert.Equal(87u, Nri.ConvertNRIFormatToDXGI(Format.BGRA8_UNORM));
        Assert.Equal(44u, Nri.ConvertNRIFormatToVK(Format.BGRA8_UNORM));
        Assert.Equal(40u, Nri.ConvertNRIFormatToDXGI(Format.D32_SFLOAT));
        Assert.Equal(126u, Nri.ConvertNRIFormatToVK(Format.D32_SFLOAT));
        Assert.Equal(98u, Nri.ConvertNRIFormatToDXGI(Format.BC7_RGBA_UNORM));
        Assert.Equal(145u, Nri.ConvertNRIFormatToVK(Format.BC7_RGBA_UNORM));
        Assert.Equal(Format.UNKNOWN, Nri.ConvertDXGIFormatToNRI(9999));
        Assert.Equal(Format.UNKNOWN, Nri.ConvertVKFormatToNRI(999999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Nri.ConvertNRIFormatToVK(Format.MAX_NUM));

        var dxgiMissing = new List<Format>();
        var vkLost = new List<Format>();
        foreach (var f in AllFormats())
        {
            var dxgi = Nri.ConvertNRIFormatToDXGI(f);
            if (dxgi == 0) dxgiMissing.Add(f);
            else if (f == Format.RG32_SINT) Assert.Equal(Format.RGB32_SINT, Nri.ConvertDXGIFormatToNRI(dxgi));
            else Assert.Equal(f, Nri.ConvertDXGIFormatToNRI(dxgi));

            var vk = Nri.ConvertNRIFormatToVK(f);
            Assert.NotEqual(0u, vk); // every NRI format has a VkFormat
            var back = Nri.ConvertVKFormatToNRI(vk);
            if (back == Format.UNKNOWN) vkLost.Add(f);
            else Assert.Equal(f, back);
        }

        Record($"no DXGI format: {string.Join(", ", dxgiMissing)}");
        Record($"VkFormat not mapped back: {string.Join(", ", vkLost)}");
        Assert.All(dxgiMissing, f => Assert.True(f.ToString().StartsWith("ETC2_") || f.ToString().StartsWith("ASTC_"), $"{f} has no DXGI format"));
        Assert.Equal(AllFormats().Where(IsBlockCompressed), vkLost);
    }

    [Fact]
    public void FormatProps_describe_each_format_of_the_generated_enum()
    {
        foreach (var f in AllFormats())
        {
            ref readonly var p = ref Nri.GetFormatProps(f);
            Assert.Equal(f, p.format);
            Assert.Equal(f.ToString(), Nri.GetFormatName(f));
            Assert.True(p.stride > 0, $"{f}: stride 0");
            Assert.True(p.blockWidth >= 1 && p.blockHeight >= 1, $"{f}: block {p.blockWidth}x{p.blockHeight}");
        }

        ref readonly var rgba8 = ref Nri.GetFormatProps(Format.RGBA8_UNORM);
        Assert.Equal((4, 8, 8, 8, 8), ((int)rgba8.stride, (int)rgba8.redBits, (int)rgba8.greenBits, (int)rgba8.blueBits, (int)rgba8.alphaBits));
        Assert.Equal((1, 0, 0), ((int)rgba8.isNorm, (int)rgba8.isSrgb, (int)rgba8.isCompressed));
        Assert.Equal(1, (int)Nri.GetFormatProps(Format.BGRA8_UNORM).isBgr);
        Assert.Equal(1, (int)Nri.GetFormatProps(Format.RGBA8_SRGB).isSrgb);
        ref readonly var bc7 = ref Nri.GetFormatProps(Format.BC7_RGBA_UNORM);
        Assert.Equal((16, 4, 4, 1), ((int)bc7.stride, (int)bc7.blockWidth, (int)bc7.blockHeight, (int)bc7.isCompressed));
        ref readonly var depth = ref Nri.GetFormatProps(Format.D32_SFLOAT_S8_UINT);
        Assert.Equal((1, 1, 1), ((int)depth.isDepth, (int)depth.isStencil, (int)depth.isFloat));
        Assert.Throws<ArgumentOutOfRangeException>(() => Nri.GetFormatProps(Format.MAX_NUM).stride);
    }

    [Fact]
    public void Graphics_API_names_are_NRIs()
    {
        Assert.Equal("VK", Nri.GetGraphicsAPIString(GraphicsAPI.VK));
        Assert.Equal("D3D12", Nri.GetGraphicsAPIString(GraphicsAPI.D3D12));
        Assert.Equal("NONE", Nri.GetGraphicsAPIString(GraphicsAPI.NONE));
        Assert.Equal("UNKNOWN", Nri.GetGraphicsAPIString((GraphicsAPI)200));
    }

    /// <summary>
    /// <c>nriFitPipelineLayoutSettingsIntoDeviceLimits</c> is arithmetic on a DeviceDesc, so the
    /// limits are made by hand. D3D12: 256 root-signature bytes, minus 8 for draw-parameter
    /// emulation, minus 4 per descriptor range, then constants or root descriptors first by
    /// preference. Vulkan: one descriptor set reserved when root descriptors exist, root samplers
    /// sharing the push-descriptor budget with root descriptors.
    /// </summary>
    [Fact]
    public void Pipeline_layout_settings_are_clamped_to_the_device_limits_in_NRIs_order()
    {
        var limits = new DeviceDesc { graphicsAPI = GraphicsAPI.D3D12 };
        limits.pipelineLayout.descriptorSetMaxNum = 8;
        limits.pipelineLayout.rootConstantMaxSize = 256;
        limits.pipelineLayout.rootDescriptorMaxNum = 64;
        limits.pipelineLayout.rootSamplerMaxNum = 16;
        var wanted = new PipelineLayoutSettingsDesc
        {
            descriptorSetNum = 4, descriptorRangeNum = 40, rootConstantSize = 128, rootDescriptorNum = 16,
            enableD3D12DrawParametersEmulation = 1,
        };

        // 256 - 8 = 248; 40 ranges cost 160, leaving 88: constants take all of it, descriptors none.
        var fitted = Nri.FitPipelineLayoutSettingsIntoDeviceLimits(limits, wanted);
        Assert.Equal((4u, 40u, 88u, 0u), (fitted.descriptorSetNum, fitted.descriptorRangeNum, fitted.rootConstantSize, fitted.rootDescriptorNum));
        Assert.Equal(1, (int)fitted.enableD3D12DrawParametersEmulation);   // preferences are passed through

        // Preferring root descriptors: 11 of them (88 / 8), no room left for constants.
        fitted = Nri.FitPipelineLayoutSettingsIntoDeviceLimits(limits, wanted with { preferRootDescriptorsOverConstants = 1 });
        Assert.Equal((88u / 8, 0u), (fitted.rootDescriptorNum, fitted.rootConstantSize));

        // Vulkan: 9 sets into 8 with one reserved for root descriptors; 16 root descriptors use the
        // whole 16-entry push-descriptor budget, so the 4 root samplers go.
        limits.graphicsAPI = GraphicsAPI.VK;
        fitted = Nri.FitPipelineLayoutSettingsIntoDeviceLimits(limits, new PipelineLayoutSettingsDesc { descriptorSetNum = 9, rootDescriptorNum = 16, rootSamplerNum = 4, rootConstantSize = 300 });
        Assert.Equal((7u, 16u, 0u, 256u), (fitted.descriptorSetNum, fitted.rootDescriptorNum, fitted.rootSamplerNum, fitted.rootConstantSize));
    }

    /// <summary>
    /// The host annotations and the thread name are NVTX calls on the calling thread, not device
    /// work, and have nothing to read back; the live-object report goes to the debugger. So this
    /// measures only that they return, and that the device beside them still validates clean and
    /// copies correctly afterwards.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Annotations_thread_name_and_live_object_report_leave_the_device_working(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        Nri.SetThreadName("PrismNRI test thread");
        using (Nri.Annotate("FlatExportTests", 0xFF00FF00))
        {
            Nri.BeginAnnotation("nested");
            Nri.Annotation("event", 0xFFFF0000);
            Nri.EndAnnotation();
            Nri.ReportLiveObjects();
        }

        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 64 });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 64 });
        var pattern = Enumerable.Range(0, 64).Select(i => (byte)(i * 7)).ToArray();
        pattern.CopyTo(upload.Map());
        upload.Unmap();
        Run(device, cmd => cmd.CopyBuffer(readback, 0, upload, 0, 64));
        Assert.Equal(pattern, readback.Map().ToArray());
        readback.Unmap();
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
