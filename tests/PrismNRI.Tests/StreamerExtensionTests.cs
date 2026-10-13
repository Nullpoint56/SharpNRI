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
