// Texture-to-texture copies (CmdCopyTexture: whole, regions, across subresources, across formats
// of one family, block-compressed), storage clears (CmdClearStorage on typed buffer views and
// storage texture views), the single global barrier (CmdBarrier with one GlobalBarrierDesc), and
// synchronous host-memory texture I/O (UploadHostMemoryToTexture, ReadbackTextureToHostMemory).
//
// Every texture starts from bytes the test chose, uploaded with CmdUploadBufferToTexture or with a
// host copy, and ends in a readback compared byte for byte; regions are checked by comparing the
// rest of the destination with what it held before.

using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;
using static SharpNRI.Tests.GpuWork;

namespace SharpNRI.Tests;

public sealed class TextureCopyTests
{
    private static readonly AccessLayoutStage ClearStorageTarget = new() { access = AccessBits.CLEAR_STORAGE, layout = Layout.SHADER_RESOURCE_STORAGE, stages = StageBits.CLEAR_STORAGE };
    private static readonly AccessStage ClearStorageBuffer = new() { access = AccessBits.CLEAR_STORAGE, stages = StageBits.CLEAR_STORAGE };
    private static readonly AccessLayoutStage HostWrite = new() { access = AccessBits.HOST_WRITE, layout = Layout.GENERAL, stages = StageBits.HOST };
    private static readonly AccessLayoutStage HostRead = new() { access = AccessBits.HOST_READ, layout = Layout.GENERAL, stages = StageBits.HOST };

    public static TheoryData<Backend, Format, Format, bool> GpuBackendsAndFormatPairs
    {
        get
        {
            var data = new TheoryData<Backend, Format, Format, bool>();
            foreach (var api in new[] { GraphicsAPI.VK, GraphicsAPI.D3D12 })
            {
                data.Add(new Backend(api), Format.RGBA8_UNORM, Format.RGBA8_SRGB, true);
                data.Add(new Backend(api), Format.R32_SFLOAT, Format.R32_UINT, false);
            }
            return data;
        }
    }

    /// <summary>
    /// CmdCopyTexture with no regions copies every subresource: two mips of two layers of an RGBA8
    /// texture, each uploaded with its own bytes, arrive in a second texture of the same desc, over
    /// a background that the copy must replace.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_whole_texture_copy_reproduces_every_subresource(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        (ushort Mip, ushort Layer)[] all = [(0, 0), (1, 0), (0, 1), (1, 1)];
        using var source = CreateTexture(device, Format.RGBA8_UNORM, 16, 8, mips: 2, layers: 2);
        using var destination = CreateTexture(device, Format.RGBA8_UNORM, 16, 8, mips: 2, layers: 2);
        using var sourceUpload = new TextureUpload(device, source, all, s => Pattern(s + 1, Size(source, all[s])));
        using var background = new TextureUpload(device, destination, all, s => Fill(0x5A, Size(destination, all[s])));
        using var readback = new TextureReadback(device, destination, all);

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(source, Undefined, TextureCopyDestination), new NriTextureBarrier(destination, Undefined, TextureCopyDestination)]);
            sourceUpload.Record(cmd);
            background.Record(cmd);
            cmd.Barrier([], [], [new NriTextureBarrier(source, TextureCopyDestination, TextureCopySource), new NriTextureBarrier(destination, TextureCopyDestination, TextureCopyDestination)]);
            cmd.CopyTexture(destination, null, source, null);
            cmd.Barrier(new NriTextureBarrier(destination, TextureCopyDestination, TextureCopySource));
            readback.Record(cmd);
        });

        var images = readback.Read();
        for (var s = 0; s < all.Length; s++)
            AssertBytes(Pattern(s + 1, Size(source, all[s])), images[s], $"mip {all[s].Mip} layer {all[s].Layer}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdCopyTexture with regions: a 5x3 region at (1, 2) of the source lands at (8, 9) of the
    /// destination, and every other destination texel keeps its background.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_region_copy_moves_only_the_region(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        const int size = 16;
        using var source = CreateTexture(device, Format.RGBA8_UNORM, size, size);
        using var destination = CreateTexture(device, Format.RGBA8_UNORM, size, size);
        var pattern = Pattern(7, size * size * 4);
        using var sourceUpload = new TextureUpload(device, source, [(0, 0)], _ => pattern);
        using var background = new TextureUpload(device, destination, [(0, 0)], _ => Fill(0x5A, size * size * 4));
        using var readback = new TextureReadback(device, destination, [(0, 0)]);

        var from = new TextureRegionDesc { x = 1, y = 2, width = 5, height = 3, depth = 1, planes = PlaneBits.COLOR };
        var to = new TextureRegionDesc { x = 8, y = 9, depth = 1, planes = PlaneBits.COLOR };

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(source, Undefined, TextureCopyDestination), new NriTextureBarrier(destination, Undefined, TextureCopyDestination)]);
            sourceUpload.Record(cmd);
            background.Record(cmd);
            cmd.Barrier([], [], [new NriTextureBarrier(source, TextureCopyDestination, TextureCopySource), new NriTextureBarrier(destination, TextureCopyDestination, TextureCopyDestination)]);
            cmd.CopyTexture(destination, to, source, from);
            cmd.Barrier(new NriTextureBarrier(destination, TextureCopyDestination, TextureCopySource));
            readback.Record(cmd);
        });

        var expected = Fill(0x5A, size * size * 4);
        for (var y = 0; y < from.height; y++)
            pattern.AsSpan(((from.y + y) * size + from.x) * 4, from.width * 4).CopyTo(expected.AsSpan(((to.y + y) * size + to.x) * 4));
        AssertBytes(expected, readback.Read()[0], "destination");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdCopyTexture between subresources: mip 1 (8x8, region extent WHOLE_SIZE) of a two-mip
    /// source lands in layer 1 of a two-layer 8x8 destination; layer 0 keeps its background.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_copy_between_subresources_takes_mip_1_into_layer_1(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        (ushort Mip, ushort Layer)[] mips = [(0, 0), (1, 0)];
        (ushort Mip, ushort Layer)[] layers = [(0, 0), (0, 1)];
        using var source = CreateTexture(device, Format.RGBA8_UNORM, 16, 16, mips: 2);
        using var destination = CreateTexture(device, Format.RGBA8_UNORM, 8, 8, layers: 2);
        using var sourceUpload = new TextureUpload(device, source, mips, s => Pattern(11 + s, Size(source, mips[s])));
        using var background = new TextureUpload(device, destination, layers, s => Fill(0x5A, Size(destination, layers[s])));
        using var readback = new TextureReadback(device, destination, layers);

        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(source, Undefined, TextureCopyDestination), new NriTextureBarrier(destination, Undefined, TextureCopyDestination)]);
            sourceUpload.Record(cmd);
            background.Record(cmd);
            cmd.Barrier([], [], [new NriTextureBarrier(source, TextureCopyDestination, TextureCopySource), new NriTextureBarrier(destination, TextureCopyDestination, TextureCopyDestination)]);
            cmd.CopyTexture(destination, new TextureRegionDesc { layerOffset = 1, planes = PlaneBits.COLOR }, source, new TextureRegionDesc { mipOffset = 1, planes = PlaneBits.COLOR });
            cmd.Barrier(new NriTextureBarrier(destination, TextureCopyDestination, TextureCopySource));
            readback.Record(cmd);
        });

        var images = readback.Read();
        AssertBytes(Fill(0x5A, Size(destination, layers[0])), images[0], "destination layer 0");
        AssertBytes(Pattern(12, Size(source, mips[1])), images[1], "destination layer 1 (source mip 1)");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdCopyTexture between two formats of one family keeps the bits: RGBA8_UNORM into RGBA8_SRGB
    /// as a whole-texture copy, R32_SFLOAT into R32_UINT as a region copy, with source words that
    /// are NaNs with payloads, negative zero and a denormal as floats. NRI creates D3D12 textures
    /// TYPELESS and Vulkan images MUTABLE_FORMAT, and neither copy converts.
    /// </summary>
    [Theory]
    [MemberData(nameof(GpuBackendsAndFormatPairs))]
    public void A_copy_between_formats_of_one_family_keeps_the_bits(Backend backend, Format from, Format to, bool whole)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        foreach (var format in new[] { from, to })
            Assert.SkipWhen((device.GetFormatSupport(format) & FormatSupportBits.TEXTURE) == 0, $"{backend}: {format} lacks TEXTURE ({device.GetFormatSupport(format)})");

        const int size = 8;
        using var source = CreateTexture(device, from, size, size);
        using var destination = CreateTexture(device, to, size, size);
        var bits = Pattern(23, size * size * 4);
        uint[] specials = [0x7FC00001, 0xFFBADBAD, 0x80000000, 0x00000001, 0x7F800000];
        MemoryMarshal.Cast<uint, byte>(specials).CopyTo(bits);
        using var sourceUpload = new TextureUpload(device, source, [(0, 0)], _ => bits);
        using var readback = new TextureReadback(device, destination, [(0, 0)]);

        var region = new TextureRegionDesc { width = size, height = size, depth = 1, planes = PlaneBits.COLOR };
        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(source, Undefined, TextureCopyDestination), new NriTextureBarrier(destination, Undefined, TextureCopyDestination)]);
            sourceUpload.Record(cmd);
            cmd.Barrier(new NriTextureBarrier(source, TextureCopyDestination, TextureCopySource));
            if (whole) cmd.CopyTexture(destination, null, source, null);
            else cmd.CopyTexture(destination, region, source, region);
            cmd.Barrier(new NriTextureBarrier(destination, TextureCopyDestination, TextureCopySource));
            readback.Record(cmd);
        });

        AssertBytes(bits, readback.Read()[0], $"{to} copied from {from}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdCopyTexture of a block-compressed region (BC1, 8 bytes per 4x4 block): the 8x4 texels at
    /// (4, 8), two blocks, land at (8, 4) of a 16x16 destination, block for block, and the other
    /// fourteen blocks keep their background.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_block_compressed_region_copy_moves_whole_blocks(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.textureCompressionBC == 0, $"{backend}: features.textureCompressionBC is false");
        Assert.SkipWhen((device.GetFormatSupport(Format.BC1_RGBA_UNORM) & FormatSupportBits.TEXTURE) == 0, $"{backend}: BC1_RGBA_UNORM lacks TEXTURE");

        const int size = 16, blocks = size / 4, blockBytes = 8;
        using var source = CreateTexture(device, Format.BC1_RGBA_UNORM, size, size);
        using var destination = CreateTexture(device, Format.BC1_RGBA_UNORM, size, size);
        var pattern = Pattern(31, blocks * blocks * blockBytes);
        using var sourceUpload = new TextureUpload(device, source, [(0, 0)], _ => pattern);
        using var background = new TextureUpload(device, destination, [(0, 0)], _ => Fill(0x5A, blocks * blocks * blockBytes));
        using var readback = new TextureReadback(device, destination, [(0, 0)]);

        var from = new TextureRegionDesc { x = 4, y = 8, width = 8, height = 4, depth = 1, planes = PlaneBits.COLOR };
        var to = new TextureRegionDesc { x = 8, y = 4, depth = 1, planes = PlaneBits.COLOR };
        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(source, Undefined, TextureCopyDestination), new NriTextureBarrier(destination, Undefined, TextureCopyDestination)]);
            sourceUpload.Record(cmd);
            background.Record(cmd);
            cmd.Barrier([], [], [new NriTextureBarrier(source, TextureCopyDestination, TextureCopySource), new NriTextureBarrier(destination, TextureCopyDestination, TextureCopyDestination)]);
            cmd.CopyTexture(destination, to, source, from);
            cmd.Barrier(new NriTextureBarrier(destination, TextureCopyDestination, TextureCopySource));
            readback.Record(cmd);
        });

        // Block rows of blocks * 8 bytes; the region is source blocks (1..2, 2) into destination blocks (2..3, 1).
        var expected = Fill(0x5A, blocks * blocks * blockBytes);
        pattern.AsSpan((2 * blocks + 1) * blockBytes, 2 * blockBytes).CopyTo(expected.AsSpan((1 * blocks + 2) * blockBytes));
        AssertBytes(expected, readback.Read()[0], "BC1 destination blocks");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdClearStorage on typed storage buffer views: an R32_UINT view of bytes [256, 768) of a
    /// 1 KiB buffer takes 0xDEADBEEF in every word and the bytes outside the view keep theirs; an
    /// R32_SFLOAT view of a whole buffer takes -1.5 in every word. The views sit in a bound
    /// descriptor set, where NRI's D3D12 backend finds the GPU handle ClearUnorderedAccessView needs.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void ClearStorage_fills_a_typed_buffer_view_and_only_its_range(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        foreach (var format in new[] { Format.R32_UINT, Format.R32_SFLOAT })
            Assert.SkipWhen((device.GetFormatSupport(format) & FormatSupportBits.STORAGE_BUFFER) == 0, $"{backend}: {format} lacks STORAGE_BUFFER");

        const int uintBytes = 1024, floatBytes = 256, viewOffset = 256, viewSize = 512;
        using var uints = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = uintBytes, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var floats = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = floatBytes, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = uintBytes + floatBytes });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = uintBytes + floatBytes });
        var before = Pattern(41, uintBytes + floatBytes);
        before.CopyTo(upload.Map());
        upload.Unmap();

        using var uintView = uints.CreateView(BufferView.STORAGE_BUFFER, viewOffset, viewSize, Format.R32_UINT);
        using var floatView = floats.CreateView(BufferView.STORAGE_BUFFER, 0, NriApi.WHOLE_SIZE, Format.R32_SFLOAT);
        using var layout = StorageLayout(device, DescriptorType.STORAGE_BUFFER);
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageBufferMaxNum = 2 });
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.UpdateRange(0, 0, [uintView, floatView]);

        var deadBeef = new Color { ui = new Color32ui { x = 0xDEADBEEF } };
        var minusOneAndHalf = new Color { f = new Color32f { x = -1.5f } };
        Run(device, cmd =>
        {
            cmd.CopyBuffer(uints, 0, upload, 0, uintBytes);
            cmd.CopyBuffer(floats, 0, upload, uintBytes, floatBytes);
            cmd.Barrier([], [new NriBufferBarrier(uints, CopyDestination, ClearStorageBuffer), new NriBufferBarrier(floats, CopyDestination, ClearStorageBuffer)], []);
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.ClearStorage(uintView, deadBeef, 0, 0, 0);
            cmd.ClearStorage(floatView, minusOneAndHalf, 0, 0, 1);
            cmd.Barrier([], [new NriBufferBarrier(uints, ClearStorageBuffer, CopySource), new NriBufferBarrier(floats, ClearStorageBuffer, CopySource)], []);
            cmd.CopyBuffer(readback, 0, uints, 0, uintBytes);
            cmd.CopyBuffer(readback, uintBytes, floats, 0, floatBytes);
        }, pool);

        var after = readback.Map().ToArray();
        readback.Unmap();
        var expected = before.ToArray();
        MemoryMarshal.Cast<byte, uint>(expected.AsSpan(viewOffset, viewSize)).Fill(0xDEADBEEF);
        MemoryMarshal.Cast<byte, float>(expected.AsSpan(uintBytes, floatBytes)).Fill(-1.5f);
        AssertBytes(expected[..uintBytes], after[..uintBytes], "R32_UINT buffer, view over [256, 768)");
        AssertBytes(expected[uintBytes..], after[uintBytes..], "R32_SFLOAT buffer, view over all of it");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdClearStorage on storage texture views: an R32_UINT view of mip 1 of a two-mip texture
    /// takes 0x12345678 there and mip 0 keeps its bytes; an RGBA32_SFLOAT array view of layer 1 of a
    /// two-layer texture takes (1.5, -2.25, 0.125, 1000) there and layer 0 keeps its bytes.
    /// Each view names exactly one subresource, the layer through STORAGE_TEXTURE_ARRAY (a
    /// STORAGE_TEXTURE view of a layer addresses it too, since NRI v191:
    /// <see cref="A_non_array_view_of_a_layer_other_than_the_first_addresses_that_layer"/>). A view of two mips
    /// clears both on Vulkan (vkCmdClearColorImage over the view's levels) and the first on D3D12, a
    /// UAV being one mip (measured, no message from either), so each view here is one mip.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void ClearStorage_fills_a_storage_texture_view_and_only_its_subresource(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        foreach (var format in new[] { Format.R32_UINT, Format.RGBA32_SFLOAT })
            Assert.SkipWhen((device.GetFormatSupport(format) & FormatSupportBits.STORAGE_TEXTURE) == 0, $"{backend}: {format} lacks STORAGE_TEXTURE");

        (ushort Mip, ushort Layer)[] mips = [(0, 0), (1, 0)];
        (ushort Mip, ushort Layer)[] layers = [(0, 0), (0, 1)];
        using var uints = CreateTexture(device, Format.R32_UINT, 8, 8, mips: 2, usage: TextureUsageBits.SHADER_RESOURCE_STORAGE);
        using var floats = CreateTexture(device, Format.RGBA32_SFLOAT, 4, 4, layers: 2, usage: TextureUsageBits.SHADER_RESOURCE_STORAGE);
        using var uintUpload = new TextureUpload(device, uints, mips, s => Pattern(51 + s, Size(uints, mips[s])));
        using var floatUpload = new TextureUpload(device, floats, layers, s => Pattern(61 + s, Size(floats, layers[s])));
        using var uintReadback = new TextureReadback(device, uints, mips);
        using var floatReadback = new TextureReadback(device, floats, layers);

        using var mip1 = uints.CreateView(new TextureViewDesc { type = TextureView.STORAGE_TEXTURE, format = Format.R32_UINT, mipOffset = 1, mipNum = 1, layerNum = 1, planes = PlaneBits.ALL });
        using var layer1 = floats.CreateView(new TextureViewDesc { type = TextureView.STORAGE_TEXTURE_ARRAY, format = Format.RGBA32_SFLOAT, mipNum = 1, layerOffset = 1, layerNum = 1, planes = PlaneBits.ALL });
        using var layout = StorageLayout(device, DescriptorType.STORAGE_TEXTURE);
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageTextureMaxNum = 2 });
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.UpdateRange(0, 0, [mip1, layer1]);

        var uintValue = new Color { ui = new Color32ui { x = 0x12345678 } };
        var floatValue = new Color { f = new Color32f { x = 1.5f, y = -2.25f, z = 0.125f, w = 1000f } };
        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(uints, Undefined, TextureCopyDestination), new NriTextureBarrier(floats, Undefined, TextureCopyDestination)]);
            uintUpload.Record(cmd);
            floatUpload.Record(cmd);
            cmd.Barrier([], [], [new NriTextureBarrier(uints, TextureCopyDestination, ClearStorageTarget), new NriTextureBarrier(floats, TextureCopyDestination, ClearStorageTarget)]);
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.ClearStorage(mip1, uintValue, 0, 0, 0);
            cmd.ClearStorage(layer1, floatValue, 0, 0, 1);
            cmd.Barrier([], [], [new NriTextureBarrier(uints, ClearStorageTarget, TextureCopySource), new NriTextureBarrier(floats, ClearStorageTarget, TextureCopySource)]);
            uintReadback.Record(cmd);
            floatReadback.Record(cmd);
        }, pool);

        var uintImages = uintReadback.Read();
        var floatImages = floatReadback.Read();
        var clearedUints = new byte[Size(uints, mips[1])];
        MemoryMarshal.Cast<byte, uint>(clearedUints).Fill(0x12345678);
        var clearedFloats = new byte[Size(floats, layers[1])];
        var texels = MemoryMarshal.Cast<byte, float>(clearedFloats);
        for (var i = 0; i < texels.Length; i += 4) (texels[i], texels[i + 1], texels[i + 2], texels[i + 3]) = (1.5f, -2.25f, 0.125f, 1000f);
        AssertBytes(Pattern(51, Size(uints, mips[0])), uintImages[0], "R32_UINT mip 0, outside the view");
        AssertBytes(clearedUints, uintImages[1], "R32_UINT mip 1, the view");
        AssertBytes(Pattern(61, Size(floats, layers[0])), floatImages[0], "RGBA32_SFLOAT layer 0, outside the view");
        AssertBytes(clearedFloats, floatImages[1], "RGBA32_SFLOAT layer 1, the view");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A non-array view of a layer other than the first addresses that layer on both backends: a
    /// STORAGE_TEXTURE view with layerOffset 1 of a two-layer RGBA32_SFLOAT texture, cleared with
    /// CmdClearStorage, fills layer 1 and leaves layer 0's bytes; TEXTURE with layerOffset 1 and
    /// TEXTURE_CUBE with layerOffset 6 of a twelve-layer texture are created without a message. NRI
    /// v190 dropped layerOffset in D3D12's non-array views (N5): the same clear filled layer 0 there and
    /// layer 1 on Vulkan, with no message from either layer. NRI #306 (71d3c586, v191) builds such a
    /// view on D3D12 as a one-layer array view.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_non_array_view_of_a_layer_other_than_the_first_addresses_that_layer(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen((device.GetFormatSupport(Format.RGBA32_SFLOAT) & FormatSupportBits.STORAGE_TEXTURE) == 0, $"{backend}: RGBA32_SFLOAT lacks STORAGE_TEXTURE");

        (ushort Mip, ushort Layer)[] layers = [(0, 0), (0, 1)];
        using var floats = CreateTexture(device, Format.RGBA32_SFLOAT, 4, 4, layers: 2, usage: TextureUsageBits.SHADER_RESOURCE | TextureUsageBits.SHADER_RESOURCE_STORAGE);
        using var cubes = CreateTexture(device, Format.RGBA8_UNORM, 4, 4, layers: 12);
        using var upload = new TextureUpload(device, floats, layers, s => Pattern(71 + s, Size(floats, layers[s])));
        using var readback = new TextureReadback(device, floats, layers);

        using var storageLayer1 = floats.CreateView(new TextureViewDesc { type = TextureView.STORAGE_TEXTURE, format = Format.RGBA32_SFLOAT, mipNum = 1, layerOffset = 1, layerNum = 1, planes = PlaneBits.ALL });
        using var textureLayer1 = floats.CreateView(new TextureViewDesc { type = TextureView.TEXTURE, format = Format.RGBA32_SFLOAT, mipNum = 1, layerOffset = 1, layerNum = 1, planes = PlaneBits.ALL });
        using var cube1 = cubes.CreateView(new TextureViewDesc { type = TextureView.TEXTURE_CUBE, format = Format.RGBA8_UNORM, mipNum = 1, layerOffset = 6, layerNum = 6, planes = PlaneBits.ALL });
        using var layout = StorageLayout(device, DescriptorType.STORAGE_TEXTURE);
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageTextureMaxNum = 2 }); // StorageLayout's range holds two
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.UpdateRange(0, 0, [storageLayer1, storageLayer1]); // both slots of the range: D3D12 requires a static range initialised (646)

        var value = new Color { f = new Color32f { x = 1.5f, y = -2.25f, z = 0.125f, w = 1000f } };
        Run(device, cmd =>
        {
            cmd.Barrier([], [], [new NriTextureBarrier(floats, Undefined, TextureCopyDestination)]);
            upload.Record(cmd);
            cmd.Barrier([], [], [new NriTextureBarrier(floats, TextureCopyDestination, ClearStorageTarget)]);
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.ClearStorage(storageLayer1, value, 0, 0, 0);
            cmd.Barrier([], [], [new NriTextureBarrier(floats, ClearStorageTarget, TextureCopySource)]);
            readback.Record(cmd);
        }, pool);

        var images = readback.Read();
        var cleared = new byte[Size(floats, layers[1])];
        var texels = MemoryMarshal.Cast<byte, float>(cleared);
        for (var i = 0; i < texels.Length; i += 4) (texels[i], texels[i + 1], texels[i + 2], texels[i + 3]) = (1.5f, -2.25f, 0.125f, 1000f);
        AssertBytes(Pattern(71, Size(floats, layers[0])), images[0], "layer 0, outside the view");
        AssertBytes(cleared, images[1], "layer 1, the STORAGE_TEXTURE view's layer");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CmdBarrier with one GlobalBarrierDesc orders a compute write before a copy: Fill.hlsl writes
    /// 64 words into a storage buffer, a global barrier from compute-shader storage access to copy
    /// source follows, and the copy reads the words the dispatch wrote. On Vulkan the Khronos
    /// synchronization validation is switched on for this device (as in
    /// BufferTests.ZeroBuffer_is_ordered_by_a_COPY_barrier), and the same dispatch and copy with no
    /// barrier, recorded and never submitted, is the positive control: it draws a SYNC-HAZARD, the
    /// global barrier draws none.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_global_barrier_orders_a_compute_write_before_a_copy(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        string[] settings = ["VK_KHRONOS_VALIDATION_VALIDATE_SYNC", "VK_KHRONOS_VALIDATION_SYNCVAL_SHADER_ACCESSES_HEURISTIC"];
        var messages = new List<NriMessage>();
        NriDevice device;
        var previous = settings.Select(Environment.GetEnvironmentVariable).ToArray();
        if (backend.Api == GraphicsAPI.VK)
            foreach (var s in settings) Environment.SetEnvironmentVariable(s, "1");
        try
        {
            device = TestDevice.Create(backend.Api, messages);
        }
        finally
        {
            for (var i = 0; i < settings.Length; i++) Environment.SetEnvironmentVariable(settings[i], previous[i]);
        }
        using var _ = device;

        const int threads = 64; // [numthreads(64, 1, 1)] in Fill.hlsl
        const ulong size = threads * sizeof(uint);
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER }])],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "Fill.cs"), "main"),
        });
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 1 });
        using var output = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = size, structureStride = sizeof(uint), usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var view = output.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = size });
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.Update(0, 0, view);

        void Dispatch(NriCommandBuffer cmd)
        {
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.Dispatch(1);
        }

        if (backend.Api == GraphicsAPI.VK)
        {
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var allocator = queue.CreateCommandAllocator();
            using var control = allocator.CreateCommandBuffer();
            var start = messages.Count;
            control.Begin(pool);
            Dispatch(control);
            control.CopyBuffer(readback, 0, output, 0, size);
            control.End();
            var hazards = messages.Skip(start).Where(m => m.Text.Contains("SYNC-HAZARD", StringComparison.Ordinal)).ToList();
            foreach (var m in hazards) Record("control, no barrier: " + m.Text);
            Assert.SkipWhen(hazards.Count == 0, $"the Khronos synchronization validation did not activate ({settings[0]} is read from the process environment at instance creation; no SYNC-HAZARD for a dispatch and a copy with no barrier between)");
            messages.Clear();
        }

        Run(device, cmd =>
        {
            Dispatch(cmd);
            cmd.Barrier(new GlobalBarrierDesc { before = ComputeStorage, after = CopySource });
            cmd.CopyBuffer(readback, 0, output, 0, size);
        }, pool);

        var words = MemoryMarshal.Cast<byte, uint>(readback.Map()).ToArray();
        readback.Unmap();
        foreach (var m in messages.Where(m => m.IsError || m.IsWarning)) Record("message: " + m.Text);
        Assert.DoesNotContain(messages, m => m.Text.Contains("SYNC-HAZARD", StringComparison.Ordinal));
        for (var i = 0; i < threads; i++) Assert.Equal(0xC0FFEE00u + (uint)i, words[i]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// UploadHostMemoryToTexture and ReadbackTextureToHostMemory with explicit pitches on a
    /// HOST_TRANSFER texture (FormatSupportBits.HOST_COPY). The upload reads rows 44 bytes apart and
    /// a slice of 308 bytes (an extra row of filler) from host memory; a GPU readback through a buffer
    /// shows the texel bytes and no filler arrived. The host readback writes rows 52 bytes apart, and
    /// the bytes between its rows keep the sentinel they had.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Host_memory_upload_and_readback_honour_row_and_slice_pitches(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        SkipOnParavirtualHostTransfer(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireHostCopy(device, Format.RGBA8_UNORM);
        var queue = device.GetQueue(QueueType.GRAPHICS);

        const int width = 8, height = 6, rowBytes = width * 4;
        const int uploadRowPitch = 44, uploadSlicePitch = 44 * 7, readbackRowPitch = 52;
        using var texture = CreateTexture(device, Format.RGBA8_UNORM, width, height, usage: TextureUsageBits.HOST_TRANSFER);
        using var gpuReadback = new TextureReadback(device, texture, [(0, 0)]);
        var texelBytes = Pattern(71, rowBytes * height);
        var host = Fill(0xCD, uploadSlicePitch);
        for (var y = 0; y < height; y++) texelBytes.AsSpan(y * rowBytes, rowBytes).CopyTo(host.AsSpan(y * uploadRowPitch));
        var whole = new TextureRegionDesc { planes = PlaneBits.COLOR };

        Run(device, cmd => cmd.Barrier(new NriTextureBarrier(texture, Undefined, HostWrite)));
        queue.UploadHostMemoryToTexture(texture, whole, host, uploadRowPitch, uploadSlicePitch);
        Run(device, cmd =>
        {
            cmd.Barrier(new NriTextureBarrier(texture, HostWrite, TextureCopySource));
            gpuReadback.Record(cmd);
            cmd.Barrier(new NriTextureBarrier(texture, TextureCopySource, HostRead));
        });
        AssertBytes(texelBytes, gpuReadback.Read()[0], "the texture after the host upload, read by the GPU");

        var back = Fill(0x77, readbackRowPitch * height);
        queue.ReadbackTextureToHostMemory(texture, whole, back, readbackRowPitch);
        var expected = Fill(0x77, readbackRowPitch * height);
        for (var y = 0; y < height; y++) texelBytes.AsSpan(y * rowBytes, rowBytes).CopyTo(expected.AsSpan(y * readbackRowPitch));
        AssertBytes(expected, back, "host memory after the host readback, row pitch 52");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The host copies take regions: a 3x2 upload at (4, 5) over an 8x8 host-uploaded background
    /// changes those six texels only, and a host readback of the same region returns them.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_host_memory_region_upload_changes_only_the_region(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        SkipOnParavirtualHostTransfer(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireHostCopy(device, Format.RGBA8_UNORM);
        var queue = device.GetQueue(QueueType.GRAPHICS);

        const int size = 8;
        using var texture = CreateTexture(device, Format.RGBA8_UNORM, size, size, usage: TextureUsageBits.HOST_TRANSFER);
        var background = Pattern(81, size * size * 4);
        var regionBytes = Pattern(91, 3 * 2 * 4);
        var region = new TextureRegionDesc { x = 4, y = 5, width = 3, height = 2, depth = 1, planes = PlaneBits.COLOR };
        var whole = new TextureRegionDesc { planes = PlaneBits.COLOR };

        Run(device, cmd => cmd.Barrier(new NriTextureBarrier(texture, Undefined, HostWrite)));
        queue.UploadHostMemoryToTexture(texture, whole, background);
        queue.UploadHostMemoryToTexture(texture, region, regionBytes);
        Run(device, cmd => cmd.Barrier(new NriTextureBarrier(texture, HostWrite, HostRead)));

        var all = new byte[size * size * 4];
        queue.ReadbackTextureToHostMemory(texture, whole, all);
        var part = new byte[regionBytes.Length];
        queue.ReadbackTextureToHostMemory(texture, region, part);

        var expected = background.ToArray();
        for (var y = 0; y < region.height; y++)
            regionBytes.AsSpan(y * region.width * 4, region.width * 4).CopyTo(expected.AsSpan(((region.y + y) * size + region.x) * 4));
        AssertBytes(expected, all, "the whole texture after the region upload");
        AssertBytes(regionBytes, part, "the region read back alone");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The host copies refuse a span shorter than the region before NRI touches it: NRI reads
    /// (rows - 1) row pitches plus one row of texel bytes per slice from the upload span and writes
    /// as much into the readback span, whatever the span's length (measured before the binding
    /// checked: an 8x8 RGBA8 readback into a 16-byte span wrote all 256 bytes, 240 past the span).
    /// A span one byte short throws ArgumentException and the memory past it keeps its sentinel;
    /// the exact length is accepted.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Host_memory_copies_refuse_a_span_shorter_than_the_region(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        SkipOnParavirtualHostTransfer(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireHostCopy(device, Format.RGBA8_UNORM);
        var queue = device.GetQueue(QueueType.GRAPHICS);

        const int size = 8, rowPitch = 40;
        const int needed = (size - 1) * rowPitch + size * 4; // the last row ends after its texels, not its pitch
        using var texture = CreateTexture(device, Format.RGBA8_UNORM, size, size, usage: TextureUsageBits.HOST_TRANSFER);
        var whole = new TextureRegionDesc { planes = PlaneBits.COLOR };
        Run(device, cmd => cmd.Barrier(new NriTextureBarrier(texture, Undefined, HostWrite)));

        var source = Pattern(101, needed + 64);
        var upload = Assert.Throws<ArgumentException>(() => queue.UploadHostMemoryToTexture(texture, whole, source.AsSpan(0, needed - 1), rowPitch));
        Record($"{backend}: upload of {needed - 1} bytes for {needed}: {upload.Message}");
        queue.UploadHostMemoryToTexture(texture, whole, source.AsSpan(0, needed), rowPitch);
        Run(device, cmd => cmd.Barrier(new NriTextureBarrier(texture, HostWrite, HostRead)));

        var sink = Fill(0x77, needed + 64);
        var readback = Assert.Throws<ArgumentException>(() => queue.ReadbackTextureToHostMemory(texture, whole, sink.AsSpan(0, needed - 1), rowPitch));
        Record($"{backend}: readback into {needed - 1} bytes for {needed}: {readback.Message}");
        AssertBytes(Fill(0x77, needed + 64), sink, "host memory after the refused readback");

        queue.ReadbackTextureToHostMemory(texture, whole, sink.AsSpan(0, needed), rowPitch);
        AssertBytes(source[..needed].Select((b, i) => i % rowPitch < size * 4 ? b : (byte)0x77).ToArray(), sink[..needed], "host memory after the readback of exactly the region");
        AssertBytes(Fill(0x77, 64), sink[needed..], "host memory past the span");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- checks and requirements

    private static void AssertBytes(byte[] expected, byte[] actual, string what)
    {
        Assert.Equal(expected.Length, actual.Length);
        var wrong = Enumerable.Range(0, expected.Length).Where(i => expected[i] != actual[i]).ToList();
        Assert.True(wrong.Count == 0, $"{what}: {wrong.Count} of {expected.Length} bytes differ, first at {string.Join(", ", wrong.Take(6).Select(i => $"[{i}] {actual[i]:X2} (expected {expected[i]:X2})"))}");
    }

    private static void RequireHostCopy(NriDevice device, Format format) =>
        Assert.SkipWhen((device.GetFormatSupport(format) & FormatSupportBits.HOST_COPY) == 0, $"{device.GraphicsApi}: {format} lacks HOST_COPY ({device.GetFormatSupport(format)})");

    // Not a missing capability but a driver crash, measured on GitHub's macOS runner (CLAUDE.md:
    // MoltenVK and the paravirtual Mac): binding memory to a host-transfer image faults below
    // MoltenVK. The device advertises hostImageCopy, so nothing NRI reports predicts it.
    private static void SkipOnParavirtualHostTransfer(Backend backend) =>
        Assert.SkipWhen(TestDevice.IsAppleParavirtualVK(backend.Api),
            "MoltenVK on the Apple Paravirtual device crashes in vkBindImageMemory for a VK_IMAGE_USAGE_HOST_TRANSFER_BIT image "
            + "(EXC_BAD_ACCESS in AppleParavirtGPUMetalIOGPUFamily under MVKImagePlane::updateMTLTextureContent)");

    // ---------------------------------------------------------------- resources

    /// <summary>Bytes that differ from their neighbours and between seeds.</summary>
    private static byte[] Pattern(int seed, int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++) bytes[i] = (byte)(seed * 37 + i * 13 + (i >> 8) * 7);
        return bytes;
    }

    private static byte[] Fill(byte value, int length)
    {
        var bytes = new byte[length];
        bytes.AsSpan().Fill(value);
        return bytes;
    }

    private static NriTexture CreateTexture(NriDevice device, Format format, ushort width, ushort height, ushort mips = 1, ushort layers = 1, TextureUsageBits usage = TextureUsageBits.SHADER_RESOURCE) =>
        device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = usage,
            format = format,
            width = width,
            height = height,
            depth = 1,
            mipNum = mips,
            layerNum = layers,
            sampleNum = 1,
        });

    /// <summary>Set 0, space 0: one range of two descriptors of <paramref name="type"/> at register 0, compute.</summary>
    private static NriPipelineLayout StorageLayout(NriDevice device, DescriptorType type) => device.CreatePipelineLayout(new NriPipelineLayoutDesc
    {
        DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 2, descriptorType = type, shaderStages = StageBits.COMPUTE_SHADER }])],
        ShaderStages = StageBits.COMPUTE_SHADER,
    });

    /// <summary>Tightly packed bytes of one subresource: rows of blocks (texels for uncompressed formats).</summary>
    private static int Size(NriTexture texture, (ushort Mip, ushort Layer) subresource)
    {
        var (rowBytes, rows) = Extent(texture, subresource.Mip);
        return rowBytes * rows;
    }

    private static (int RowBytes, int Rows) Extent(NriTexture texture, ushort mip)
    {
        ref readonly var desc = ref texture.Desc;
        ref readonly var props = ref Nri.GetFormatProps(desc.format);
        var width = Math.Max(1, desc.width >> mip);
        var height = Math.Max(1, desc.height >> mip);
        return ((width + props.blockWidth - 1) / props.blockWidth * props.stride, (height + props.blockHeight - 1) / props.blockHeight);
    }

    private readonly record struct Part(TextureRegionDesc Region, TextureDataLayoutDesc Layout, int RowBytes, int Rows);

    /// <summary>Whole-subresource regions and their buffer layouts at the device's row and slice alignment.</summary>
    private static List<Part> Layouts(NriDevice device, NriTexture texture, (ushort Mip, ushort Layer)[] subresources, out ulong size)
    {
        ref readonly var alignment = ref device.Desc.memoryAlignment;
        ref readonly var desc = ref texture.Desc;
        var parts = new List<Part>();
        ulong offset = 0;
        foreach (var (mip, layer) in subresources)
        {
            var (rowBytes, rows) = Extent(texture, mip);
            var rowPitch = (uint)Align((ulong)rowBytes, alignment.uploadBufferTextureRow);
            var slicePitch = (uint)Align((ulong)rowPitch * (ulong)rows, alignment.uploadBufferTextureSlice);
            offset = Align(offset, alignment.uploadBufferTextureSlice);
            var width = (ushort)Math.Max(1, desc.width >> mip);
            var height = (ushort)Math.Max(1, desc.height >> mip);
            parts.Add(new Part(
                new TextureRegionDesc { width = width, height = height, depth = 1, mipOffset = mip, layerOffset = layer, planes = PlaneBits.COLOR },
                new TextureDataLayoutDesc { offset = offset, rowPitch = rowPitch, slicePitch = slicePitch },
                rowBytes, rows));
            offset += slicePitch;
        }
        size = Math.Max(offset, 4);
        return parts;
    }

    /// <summary>Uploads whole subresources through one HOST_UPLOAD buffer; the texture must be in COPY_DESTINATION.</summary>
    private sealed class TextureUpload : IDisposable
    {
        private readonly NriBuffer _buffer;
        private readonly NriTexture _texture;
        private readonly List<Part> _parts;

        public TextureUpload(NriDevice device, NriTexture texture, (ushort Mip, ushort Layer)[] subresources, Func<int, byte[]> content)
        {
            _texture = texture;
            _parts = Layouts(device, texture, subresources, out var size);
            _buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = size });
            var mapped = _buffer.Map();
            for (var s = 0; s < _parts.Count; s++)
            {
                var (_, layout, rowBytes, rows) = _parts[s];
                var bytes = content(s);
                Assert.Equal(rowBytes * rows, bytes.Length);
                for (var y = 0; y < rows; y++)
                    bytes.AsSpan(y * rowBytes, rowBytes).CopyTo(mapped[(int)(layout.offset + (ulong)y * layout.rowPitch)..]);
            }
            _buffer.Unmap();
        }

        public void Record(NriCommandBuffer cmd)
        {
            foreach (var (region, layout, _, _) in _parts) cmd.UploadBufferToTexture(_texture, region, _buffer, layout);
        }

        public void Dispose() => _buffer.Dispose();
    }

    /// <summary>Reads whole subresources back through one HOST_READBACK buffer, tightly packed; the texture must be in COPY_SOURCE.</summary>
    private sealed class TextureReadback : IDisposable
    {
        private readonly NriBuffer _buffer;
        private readonly NriTexture _texture;
        private readonly List<Part> _parts;

        public TextureReadback(NriDevice device, NriTexture texture, (ushort Mip, ushort Layer)[] subresources)
        {
            _texture = texture;
            _parts = Layouts(device, texture, subresources, out var size);
            _buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = size });
        }

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
                    mapped.Slice((int)(layout.offset + (ulong)y * layout.rowPitch), rowBytes).CopyTo(result[s].AsSpan(y * rowBytes));
            }
            _buffer.Unmap();
            return result;
        }

        public void Dispose() => _buffer.Dispose();
    }
}
