// The Streamer extension: acquisition on every backend, NONE included (its answer is part of the
// availability table); on the GPU backends real uploads through the streamer's dynamic buffer into
// a buffer and a texture, copied by CmdCopyStreamedData and read back; and, on NONE, the lifetime of
// the buffers a streamer hands out, which is the binding's bookkeeping and needs no GPU.

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class StreamerExtensionTests
{
    private static StreamerDesc Desc() => new()
    {
        constantBufferMemoryLocation = MemoryLocation.HOST_UPLOAD,
        constantBufferSize = 64 * 1024,
        dynamicBufferMemoryLocation = MemoryLocation.HOST_UPLOAD,
        dynamicBufferDesc = new BufferDesc { usage = BufferUsageBits.VERTEX | BufferUsageBits.INDEX | BufferUsageBits.CONSTANT },
        queuedFrameNum = 1,
        hostDataCapacity = 64 * 1024,
    };

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void The_extension_is_available_and_a_streamer_lives_and_dies(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        Assert.NotNull(device.TryStreamer);
        Assert.Same(device.Streamer, device.TryStreamer);

        using (var streamer = device.Streamer.CreateStreamer(Desc()))
        {
            Assert.Equal(1, device.LiveObjects);
            Assert.Equal(1u, streamer.Desc.queuedFrameNum);

            var constants = device.Streamer.GetStreamerConstantBuffer(streamer);
            Assert.NotNull(constants);
            Assert.False(constants.IsOwned);
            Assert.Same(constants, device.Streamer.GetStreamerConstantBuffer(streamer));
            constants.Dispose(); // a no-op on a buffer the streamer owns
            Assert.Equal(1, device.LiveObjects);

            device.Streamer.StreamConstantData(streamer, new System.Numerics.Vector4(1, 2, 3, 4));
            device.Streamer.EndStreamerFrame(streamer);
        }

        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Host_data_is_copied_into_the_ring(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var streamer = device.Streamer.CreateStreamer(Desc());

        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        var copy = device.Streamer.StreamHostData(streamer, data, 16);
        Assert.Equal(data, copy.ToArray());
        copy[0] = 42; // read/write until the ring wraps

        var first = device.Streamer.StreamConstantData(streamer, data);
        var second = device.Streamer.StreamConstantData(streamer, data);
        Assert.NotEqual(first, second);
        Assert.Equal(0u, first % device.Desc.memoryAlignment.constantBufferOffset);
        Assert.Equal(0u, second % device.Desc.memoryAlignment.constantBufferOffset);

        device.Streamer.EndStreamerFrame(streamer);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Streamed_chunks_are_copied_into_a_buffer_and_read_back(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var ext = device.Streamer;
        using var streamer = ext.CreateStreamer(Desc());
        using var dst = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 256 });

        var a = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();
        var b = Enumerable.Range(100, 24).Select(i => (byte)i).ToArray();
        var batch = ext.BeginStreamerCopyBatch(streamer);
        Assert.NotEqual(0UL, batch);

        var placed = ext.StreamBufferData(streamer, new NriStreamBufferDataDesc
        {
            DataChunks = [a, b],
            PlacementAlignment = 16,
            CopyBatch = batch,
            DstBuffer = dst,
            DstOffset = 32,
        });
        Assert.NotNull(placed.Buffer);
        Assert.False(placed.Buffer.IsOwned);
        Assert.Equal(0UL, placed.Offset % 16);

        // A second, single-chunk upload into the same batch.
        byte[] tail = [0xAA, 0xBB, 0xCC, 0xDD];
        var placedTail = ext.StreamBufferData(streamer, tail, 4, batch, dst, 200);
        Assert.Same(placed.Buffer, placedTail.Buffer);

        GpuWork.Run(device, cb =>
        {
            cb.Barrier(new NriBufferBarrier(dst, GpuWork.Nothing, GpuWork.CopyDestination));
            ext.CopyStreamedData(cb, streamer, batch);
        });
        ext.EndStreamerFrame(streamer);

        var bytes = dst.Map().ToArray();
        dst.Unmap();
        Assert.Equal(a, bytes.AsSpan(32, a.Length).ToArray());
        Assert.Equal(b, bytes.AsSpan(32 + a.Length, b.Length).ToArray());
        Assert.Equal(tail, bytes.AsSpan(200, tail.Length).ToArray());
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Streamed_texels_are_copied_into_a_texture_and_read_back(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var ext = device.Streamer;
        using var streamer = ext.CreateStreamer(Desc());

        const int Size = 4;
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        var rowPitch = (uint)GpuWork.Align(Size * 4, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)GpuWork.Align(rowPitch * Size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        var texels = Enumerable.Range(0, Size * Size * 4).Select(i => (byte)(i * 3)).ToArray();
        var batch = ext.BeginStreamerCopyBatch(streamer);
        var placed = ext.StreamTextureData(streamer, new NriStreamTextureDataDesc
        {
            Data = texels,
            DataRowPitch = Size * 4,
            DataSlicePitch = Size * Size * 4,
            CopyBatch = batch,
            DstTexture = texture,
        });
        Assert.NotNull(placed.Buffer);

        GpuWork.Run(device, cb =>
        {
            cb.Barrier(new NriTextureBarrier(texture, GpuWork.Undefined, GpuWork.TextureCopyDestination));
            ext.CopyStreamedData(cb, streamer, batch);
            cb.Barrier(new NriTextureBarrier(texture, GpuWork.TextureCopyDestination, GpuWork.TextureCopySource));
            cb.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = rowPitch, slicePitch = slicePitch }, texture,
                new TextureRegionDesc { width = Size, height = Size, depth = 1, planes = PlaneBits.COLOR });
        });
        ext.EndStreamerFrame(streamer);

        var mapped = readback.Map().ToArray();
        readback.Unmap();
        for (var y = 0; y < Size; y++)
            Assert.Equal(texels.AsSpan(y * Size * 4, Size * 4).ToArray(), mapped.AsSpan((int)(y * rowPitch), Size * 4).ToArray());
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// queuedFrameNum is the number of per-frame regions in the dynamic buffer, the frame being
    /// filled among them: with 2, frames 0 and 1 stream into two regions and frame 2 reuses frame
    /// 0's offset. Upstream's header says NRI adds one region for the current frame; the code has
    /// not since 6f80a7c, and patch 0011 corrects the comment.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Data_streamed_in_a_frame_is_overwritten_queuedFrameNum_frames_later(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var ext = device.Streamer;
        using var streamer = ext.CreateStreamer(Desc() with { queuedFrameNum = 2 });

        var placed = new NriBufferOffset[4];
        for (var frame = 0; frame < placed.Length; frame++)
        {
            placed[frame] = ext.StreamBufferData(streamer, [1, 2, 3, 4]);
            ext.EndStreamerFrame(streamer);
        }
        GpuWork.Record($"{backend}: frames 0-3 streamed at offsets {string.Join(", ", placed.Select(p => p.Offset))}");

        Assert.NotNull(placed[0].Buffer);
        Assert.NotEqual(placed[0].Offset, placed[1].Offset);
        Assert.Equal(placed[0], placed[2]);
        Assert.Equal(placed[1], placed[3]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// dynamicBufferDesc.size is the initial per-frame size of the dynamic buffer (patch 0012;
    /// upstream v190 ignores it and grows from empty, one committed buffer per new per-frame
    /// high-water mark, each parked and destroyed queuedFrameNum frames later). Frames that stream
    /// less than that size all get the same buffer, sized the 64 KiB-rounded per-frame size times
    /// queuedFrameNum; a frame that streams more still grows it.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_dynamic_buffer_starts_at_the_requested_per_frame_size_and_grows_only_past_it(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var ext = device.Streamer;
        const ulong perFrame = 100 * 1024; // not a multiple of NRI's 64 KiB chunk: rounds up to 128 KiB
        const uint queued = 2;
        var desc = Desc() with { queuedFrameNum = queued };
        desc.dynamicBufferDesc.size = perFrame;
        using var streamer = ext.CreateStreamer(desc);

        var data = new byte[64 * 1024];
        var first = ext.StreamBufferData(streamer, data);
        Assert.NotNull(first.Buffer);
        ext.EndStreamerFrame(streamer);
        for (var frame = 1; frame < 4; frame++)
        {
            var placed = ext.StreamBufferData(streamer, data);
            Assert.Same(first.Buffer, placed.Buffer); // under the per-frame size: no growth, the buffer from creation
            ext.EndStreamerFrame(streamer);
        }
        var initialSize = first.Buffer.Desc.size;
        Assert.Equal(128 * 1024 * (ulong)queued, initialSize);

        // 192 KiB in one frame outgrows the 128 KiB per-frame region: a new, larger buffer.
        ext.StreamBufferData(streamer, data);
        ext.StreamBufferData(streamer, data);
        var grown = ext.StreamBufferData(streamer, data);
        Assert.NotSame(first.Buffer, grown.Buffer);
        Assert.True(grown.Buffer!.Desc.size > initialSize, $"grown buffer {grown.Buffer.Desc.size} B is not larger than {initialSize} B");
        ext.EndStreamerFrame(streamer);
        GpuWork.Record($"{backend}: per-frame size {perFrame} B x {queued} frames: dynamic buffer {initialSize} B at creation, {grown.Buffer.Desc.size} B after a 192 KiB frame");

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A block-compressed texture is staged in rows of blocks, the layout CmdUploadBufferToTexture
    /// reads: every mip of a 64x64 BC1 texture, down to the 2x2 and 1x1 mips that are one partial
    /// block, is streamed with its block-row pitch, copied and read back byte for byte, and each mip
    /// takes its block footprint in the dynamic buffer (the distance to the next offset). Upstream
    /// v190 staged texel rows, 16 times the footprint (32,768 B for the 64x64 mip), reading past the
    /// end of the caller's data; the bytes still arrived intact, so only the footprint shows it
    /// (patch 0010).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_block_compressed_texture_is_staged_in_rows_of_blocks(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var support = device.GetFormatSupport(Format.BC1_RGBA_UNORM);
        Assert.SkipWhen((support & FormatSupportBits.TEXTURE) == 0, $"{backend}: BC1_RGBA_UNORM lacks TEXTURE (has {support})");
        var ext = device.Streamer;
        using var streamer = ext.CreateStreamer(Desc());

        const int Size = 64, MipNum = 7, BlockBytes = 8; // BC1: a 4x4 block in 8 bytes
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.BC1_RGBA_UNORM,
            width = Size, height = Size, depth = 1, mipNum = MipNum, layerNum = 1, sampleNum = 1,
        });

        var rowAlignment = device.Desc.memoryAlignment.uploadBufferTextureRow;
        var sliceAlignment = device.Desc.memoryAlignment.uploadBufferTextureSlice;
        var mips = new byte[MipNum][];
        var blockRows = new uint[MipNum];
        var blockRowPitch = new uint[MipNum];
        var readbackLayout = new TextureDataLayoutDesc[MipNum];
        ulong readbackSize = 0;
        for (var m = 0; m < MipNum; m++)
        {
            var blocks = (uint)(Math.Max(Size >> m, 1) + 3) / 4;
            blockRows[m] = blocks;
            blockRowPitch[m] = blocks * BlockBytes;
            mips[m] = Enumerable.Range(0, (int)(blocks * blockRowPitch[m])).Select(i => (byte)(i * 13 + m * 101 + 1)).ToArray();
            var rowPitch = (uint)GpuWork.Align(blockRowPitch[m], rowAlignment);
            var slicePitch = (uint)GpuWork.Align(rowPitch * blocks, sliceAlignment);
            readbackLayout[m] = new TextureDataLayoutDesc { offset = readbackSize, rowPitch = rowPitch, slicePitch = slicePitch };
            readbackSize += slicePitch;
        }
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = readbackSize });

        var batch = ext.BeginStreamerCopyBatch(streamer);
        var staged = new ulong[MipNum + 1];
        for (var m = 0; m < MipNum; m++)
        {
            var placed = ext.StreamTextureData(streamer, new NriStreamTextureDataDesc
            {
                Data = mips[m],
                DataRowPitch = blockRowPitch[m],
                DataSlicePitch = (uint)mips[m].Length,
                CopyBatch = batch,
                DstTexture = texture,
                DstRegion = new TextureRegionDesc { mipOffset = (ushort)m },
            });
            Assert.NotNull(placed.Buffer);
            staged[m] = placed.Offset;
        }
        staged[MipNum] = ext.StreamBufferData(streamer, [0], 1).Offset; // where the last mip ends

        GpuWork.Run(device, cb =>
        {
            cb.Barrier(new NriTextureBarrier(texture, GpuWork.Undefined, GpuWork.TextureCopyDestination));
            ext.CopyStreamedData(cb, streamer, batch);
            cb.Barrier(new NriTextureBarrier(texture, GpuWork.TextureCopyDestination, GpuWork.TextureCopySource));
            for (var m = 0; m < MipNum; m++)
                cb.ReadbackTextureToBuffer(readback, readbackLayout[m], texture, new TextureRegionDesc { mipOffset = (ushort)m, planes = PlaneBits.COLOR });
        });
        ext.EndStreamerFrame(streamer);

        var footprint = new ulong[MipNum];
        for (var m = 0; m < MipNum; m++)
        {
            footprint[m] = GpuWork.Align(GpuWork.Align(blockRowPitch[m], rowAlignment) * blockRows[m], sliceAlignment);
            GpuWork.Record($"{backend}: mip {m} ({Math.Max(Size >> m, 1)}x{Math.Max(Size >> m, 1)}, {blockRows[m]}x{blockRows[m]} blocks) staged in {staged[m + 1] - staged[m]} B, block footprint {footprint[m]} B");
        }

        var mapped = readback.Map().ToArray();
        readback.Unmap();
        for (var m = 0; m < MipNum; m++)
            for (var r = 0; r < blockRows[m]; r++)
                Assert.Equal(mips[m].AsSpan((int)(r * blockRowPitch[m]), (int)blockRowPitch[m]).ToArray(),
                    mapped.AsSpan((int)(readbackLayout[m].offset + (ulong)r * readbackLayout[m].rowPitch), (int)blockRowPitch[m]).ToArray());
        for (var m = 0; m < MipNum; m++)
            Assert.Equal(footprint[m], staged[m + 1] - staged[m]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>Object lifetime only, which is what NONE is for: no assertion here asks NRI a question.</summary>
    [Fact]
    public void Disposing_the_streamer_invalidates_the_buffers_it_handed_out()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);
        NriBuffer? constants;
        using (var streamer = device.Streamer.CreateStreamer(Desc()))
        {
            constants = device.Streamer.GetStreamerConstantBuffer(streamer);
            Assert.NotNull(constants);
            Assert.False(constants.IsDisposed);
        }
        Assert.True(constants.IsDisposed);
        Assert.Equal(0, device.LiveObjects);
    }
}
