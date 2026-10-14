// NriTexture: committed 2D textures with each usage, the desc NRI keeps (with its defaults filled
// in), views, and a 4x4 RGBA8 upload through CmdUploadBufferToTexture read back through
// CmdReadbackTextureToBuffer, with the barriers NRI's layouts require.

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class TextureTests
{
    public static TheoryData<Backend, TextureUsageBits> GpuBackendsAndUsages
    {
        get
        {
            var data = new TheoryData<Backend, TextureUsageBits>();
            foreach (var api in new[] { GraphicsAPI.VK, GraphicsAPI.D3D12 })
            foreach (var usage in new[]
            {
                TextureUsageBits.SHADER_RESOURCE, TextureUsageBits.SHADER_RESOURCE_STORAGE, TextureUsageBits.COLOR_ATTACHMENT,
                TextureUsageBits.DEPTH_STENCIL_ATTACHMENT, TextureUsageBits.SHADING_RATE_ATTACHMENT, TextureUsageBits.INPUT_ATTACHMENT,
                TextureUsageBits.HOST_TRANSFER,
            })
            {
                data.Add(new Backend(api), usage);
            }
            return data;
        }
    }

    private static TextureDesc Texture2D(TextureUsageBits usage, Format format, ushort width = 4, ushort height = 4) => new()
    {
        type = TextureType.TEXTURE_2D,
        usage = usage,
        format = format,
        width = width,
        height = height,
        depth = 1,
        mipNum = 1,
        layerNum = 1,
        sampleNum = 1,
    };

    [Theory]
    [MemberData(nameof(GpuBackendsAndUsages))]
    public void Committed_2D_texture_is_created_with_each_usage(Backend backend, TextureUsageBits usage)
    {
        TestDevice.RequireAvailable(backend);
        // Not a missing capability but a driver crash, measured on GitHub's macOS runner: binding
        // memory to a host-transfer image faults inside Apple's paravirtual Metal driver, below
        // MoltenVK, with no Vulkan error first (CLAUDE.md: MoltenVK and the paravirtual Mac). The device advertises
        // hostImageCopy, so nothing NRI reports can predict it; the skip names the device instead.
        Assert.SkipWhen(usage == TextureUsageBits.HOST_TRANSFER && TestDevice.IsAppleParavirtualVK(backend.Api),
            "MoltenVK on the Apple Paravirtual device crashes in vkBindImageMemory for a VK_IMAGE_USAGE_HOST_TRANSFER_BIT image "
            + "(EXC_BAD_ACCESS in AppleParavirtGPUMetalIOGPUFamily under MVKImagePlane::updateMTLTextureContent)");
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // The format each usage needs, and the device's answer on whether it is supported.
        var (format, required) = usage switch
        {
            TextureUsageBits.DEPTH_STENCIL_ATTACHMENT => (Format.D32_SFLOAT, FormatSupportBits.DEPTH_STENCIL_ATTACHMENT),
            TextureUsageBits.SHADING_RATE_ATTACHMENT => (Format.R8_UINT, FormatSupportBits.TEXTURE),
            TextureUsageBits.SHADER_RESOURCE_STORAGE => (Format.RGBA8_UNORM, FormatSupportBits.STORAGE_TEXTURE),
            TextureUsageBits.COLOR_ATTACHMENT or TextureUsageBits.INPUT_ATTACHMENT => (Format.RGBA8_UNORM, FormatSupportBits.COLOR_ATTACHMENT),
            TextureUsageBits.HOST_TRANSFER => (Format.RGBA8_UNORM, FormatSupportBits.HOST_COPY),
            _ => (Format.RGBA8_UNORM, FormatSupportBits.TEXTURE),
        };
        var support = device.GetFormatSupport(format);
        Assert.SkipWhen((support & required) != required, $"{backend}: {format} lacks {required} (has {support})");
        Assert.SkipWhen(usage == TextureUsageBits.SHADING_RATE_ATTACHMENT && device.Desc.tiers.shadingRate < 2,
            $"{backend}: tiers.shadingRate = {device.Desc.tiers.shadingRate}; a shading-rate attachment needs 2");

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture2D(usage, format, 16, 16));

        Assert.Equal(usage, texture.Desc.usage);
        Assert.Equal(format, texture.Desc.format);
        Assert.Equal(1, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Desc_fills_NRI_defaults_for_zero_optional_fields(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // NRIDescs.h marks height, depth, mipNum, layerNum and sampleNum "NriOptional"; every real
        // backend runs the desc through FixTextureDesc (Shared/SharedExternal.h), which raises each
        // of the five to at least 1.
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = TextureUsageBits.SHADER_RESOURCE,
            format = Format.RGBA8_UNORM,
            width = 8,
            height = 0,
        });
        var held = texture.Desc;
        Record($"{backend}: held type={held.type} usage={held.usage} format={held.format} {held.width}x{held.height}x{held.depth} mips={held.mipNum} layers={held.layerNum} samples={held.sampleNum}");

        // Measured on VK and D3D12: type, usage, format and width echo; the five optional
        // fields come back as 1.
        Assert.Equal(TextureType.TEXTURE_2D, held.type);
        Assert.Equal(TextureUsageBits.SHADER_RESOURCE, held.usage);
        Assert.Equal(Format.RGBA8_UNORM, held.format);
        Assert.Equal((ushort)8, held.width);
        Assert.Equal((ushort)1, held.height);
        Assert.Equal((ushort)1, held.depth);
        Assert.Equal((ushort)1, held.mipNum);
        Assert.Equal((ushort)1, held.layerNum);
        Assert.Equal((byte)1, held.sampleNum);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Records NONE's dummy behaviour, not a feature: NONE answers GetTextureDesc with one static
    /// desc for every texture, TEXTURE_1D, usage NONE, R8_UNORM, 1x1x1, one mip, one layer, one
    /// sample (ImplNONE.cpp; CLAUDE.md: Device creation and callbacks). NONE has no validation layer, so there are no
    /// messages to check.
    /// </summary>
    [Fact]
    public void NONE_answers_one_static_desc_for_every_texture()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = TextureUsageBits.SHADER_RESOURCE,
            format = Format.RGBA8_UNORM,
            width = 8,
            height = 0,
        });
        var held = texture.Desc;

        Assert.Equal(TextureType.TEXTURE_1D, held.type);
        Assert.Equal(TextureUsageBits.NONE, held.usage);
        Assert.Equal(Format.R8_UNORM, held.format);
        Assert.Equal((ushort)1, held.width);
        Assert.Equal((ushort)1, held.height);
        Assert.Equal((ushort)1, held.depth);
        Assert.Equal((ushort)1, held.mipNum);
        Assert.Equal((ushort)1, held.layerNum);
        Assert.Equal((byte)1, held.sampleNum);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Desc_round_trips_explicit_fields(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var desc = new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = TextureUsageBits.SHADER_RESOURCE | TextureUsageBits.COLOR_ATTACHMENT,
            format = Format.RGBA8_UNORM,
            width = 64,
            height = 32,
            depth = 1,
            mipNum = 4,
            layerNum = 3,
            sampleNum = 1,
        };
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, desc);
        var held = texture.Desc;

        Assert.Equal(desc.type, held.type);
        Assert.Equal(desc.usage, held.usage);
        Assert.Equal(desc.format, held.format);
        Assert.Equal(desc.width, held.width);
        Assert.Equal(desc.height, held.height);
        Assert.Equal(desc.depth, held.depth);
        Assert.Equal(desc.mipNum, held.mipNum);
        Assert.Equal(desc.layerNum, held.layerNum);
        Assert.Equal(desc.sampleNum, held.sampleNum);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Views_of_each_kind_are_created_for_matching_usage(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture2D(
            TextureUsageBits.SHADER_RESOURCE | TextureUsageBits.SHADER_RESOURCE_STORAGE | TextureUsageBits.COLOR_ATTACHMENT, Format.RGBA8_UNORM, 16, 16));

        using var sampled = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        using var storage = texture.CreateView(TextureView.STORAGE_TEXTURE, Format.RGBA8_UNORM);
        using var attachment = texture.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);

        Assert.All([sampled, storage, attachment], v => Assert.Equal(NriDescriptorKind.TextureView, v.Kind));
        Assert.Equal(4, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Depth_view_is_created_for_a_depth_texture(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture2D(TextureUsageBits.DEPTH_STENCIL_ATTACHMENT, Format.D32_SFLOAT, 16, 16));
        using var view = texture.CreateView(TextureView.DEPTH_STENCIL_ATTACHMENT, Format.D32_SFLOAT);

        Assert.Equal(2, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_4x4_RGBA8_pattern_uploads_and_reads_back_byte_for_byte(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // Measured on the RTX 4070 SUPER: VK reports uploadBufferTextureRow = 1 and
        // uploadBufferTextureSlice = 16; D3D12 reports 1 and 1 (inbox runtime and Agility SDK 1.619
        // alike: options13.UnrestrictedBufferTextureCopyPitchSupported), i.e. NRI sees
        // unrestricted copy pitches rather than D3D12's classic 256/512. So the 16-byte row is
        // already aligned on both; the alignment math stays for hardware that reports more.
        const int width = 4, height = 4, texel = 4;
        var rowAlignment = device.Desc.memoryAlignment.uploadBufferTextureRow;
        var sliceAlignment = device.Desc.memoryAlignment.uploadBufferTextureSlice;
        var rowPitch = (uint)Align(width * texel, rowAlignment);
        var slicePitch = (uint)Align((ulong)rowPitch * height, sliceAlignment);
        Record($"{backend}: uploadBufferTextureRow={rowAlignment} uploadBufferTextureSlice={sliceAlignment} -> rowPitch={rowPitch} slicePitch={slicePitch}");

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture2D(TextureUsageBits.SHADER_RESOURCE, Format.RGBA8_UNORM, width, height));
        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = slicePitch });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        var pattern = new byte[width * height * texel];
        for (var i = 0; i < pattern.Length; i++) pattern[i] = (byte)(0x10 + i * 3);

        var mapped = upload.Map();
        mapped.Clear();
        for (var y = 0; y < height; y++)
        {
            pattern.AsSpan(y * width * texel, width * texel).CopyTo(mapped[(int)(y * rowPitch)..]);
        }
        upload.Unmap();

        var region = new TextureRegionDesc { width = width, height = height, depth = 1, planes = PlaneBits.ALL };
        var layout = new TextureDataLayoutDesc { offset = 0, rowPitch = rowPitch, slicePitch = slicePitch };

        Run(device, cmd =>
        {
            cmd.Barrier(new NriTextureBarrier(texture, Undefined, TextureCopyDestination));
            cmd.UploadBufferToTexture(texture, region, upload, layout);
            cmd.Barrier(new NriTextureBarrier(texture, TextureCopyDestination, TextureCopySource));
            cmd.ReadbackTextureToBuffer(readback, layout, texture, region);
        });

        var bytes = readback.Map();
        var actual = new byte[pattern.Length];
        for (var y = 0; y < height; y++)
        {
            bytes.Slice((int)(y * rowPitch), width * texel).CopyTo(actual.AsSpan(y * width * texel));
        }
        readback.Unmap();

        Assert.Equal(pattern, actual);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Disposal_is_counted_per_texture_and_view(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture2D(TextureUsageBits.SHADER_RESOURCE, Format.RGBA8_UNORM));
        var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        Assert.Equal(2, device.LiveObjects);

        view.Dispose();
        Assert.Equal(1, device.LiveObjects);
        texture.Dispose();
        Assert.Equal(0, device.LiveObjects);
        Assert.Throws<ObjectDisposedException>(() => texture.Desc.width);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
