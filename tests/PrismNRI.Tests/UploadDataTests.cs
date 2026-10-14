// NriQueue.UploadData (HelperInterface::UploadData): a device-local buffer and a two-layer,
// two-mip RGBA8 texture filled in one call from tightly packed host data, left in COPY_SOURCE, then
// read back with CmdCopyBuffer / CmdReadbackTextureToBuffer and compared byte for byte, subresource
// by subresource, so the layer-major order the binding documents is what NRI reads.

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class UploadDataTests
{
    private const ushort Width = 8, Height = 8, Mips = 2, Layers = 2;

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_buffer_and_every_subresource_of_a_texture_upload_in_one_call(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);

        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 1000, usage = BufferUsageBits.SHADER_RESOURCE });
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = Width, height = Height, depth = 1, mipNum = Mips, layerNum = Layers, sampleNum = 1,
        });

        var bufferData = Enumerable.Range(0, 1000).Select(i => (byte)(i * 13 + 5)).ToArray();

        // Subresource (layer, mip) is filled with a byte pattern seeded by its index in NRI's order.
        var subresources = new NriTextureSubresourceUpload[Layers * Mips];
        var expected = new byte[Layers * Mips][];
        for (var layer = 0; layer < Layers; layer++)
        for (var mip = 0; mip < Mips; mip++)
        {
            var index = layer * Mips + mip;
            uint w = (uint)(Width >> mip), h = (uint)(Height >> mip);
            var data = new byte[w * h * 4];
            for (var i = 0; i < data.Length; i++) data[i] = (byte)(index * 64 + i * 3);
            expected[index] = data;
            subresources[index] = new NriTextureSubresourceUpload(data, SliceNum: 1, RowPitch: w * 4, SlicePitch: w * h * 4);
        }

        // The binding refuses data NRI would read past the end of.
        Assert.Throws<ArgumentException>(() => queue.UploadData([], [new NriBufferUpload(buffer, new byte[999], CopySource)]));
        Assert.Throws<ArgumentException>(() => queue.UploadData([new NriTextureUpload(texture, subresources[..3], TextureCopySource)], []));

        queue.UploadData([new NriTextureUpload(texture, subresources, TextureCopySource)], [new NriBufferUpload(buffer, bufferData, CopySource)]);

        // Read back: the buffer at 0, then each subresource at a slice-aligned offset.
        var rowAlignment = device.Desc.memoryAlignment.uploadBufferTextureRow;
        var sliceAlignment = device.Desc.memoryAlignment.uploadBufferTextureSlice;
        var offsets = new ulong[Layers * Mips];
        var pitches = new uint[Layers * Mips];
        var next = Align(1000, sliceAlignment);
        for (var index = 0; index < offsets.Length; index++)
        {
            var w = (uint)(Width >> (index % Mips));
            var h = (uint)(Height >> (index % Mips));
            pitches[index] = (uint)Align(w * 4, rowAlignment);
            offsets[index] = next;
            next = Align(next + (ulong)pitches[index] * h, sliceAlignment);
        }
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = next });

        Run(device, cmd =>
        {
            cmd.Barrier(new NriBufferBarrier(readback, Nothing, CopyDestination));
            cmd.CopyBuffer(readback, 0, buffer, 0, 1000);
            for (var index = 0; index < offsets.Length; index++)
            {
                var mip = index % Mips;
                var region = new TextureRegionDesc
                {
                    width = (ushort)(Width >> mip), height = (ushort)(Height >> mip), depth = 1,
                    mipOffset = (byte)mip, layerOffset = (ushort)(index / Mips), planes = PlaneBits.ALL,
                };
                var h = (uint)(Height >> mip);
                cmd.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { offset = offsets[index], rowPitch = pitches[index], slicePitch = pitches[index] * h }, texture, region);
            }
        });

        var bytes = readback.Map().ToArray();
        readback.Unmap();
        Assert.Equal(bufferData, bytes[..1000]);
        for (var index = 0; index < offsets.Length; index++)
        {
            var mip = index % Mips;
            var rowBytes = (Width >> mip) * 4;
            var actual = new byte[expected[index].Length];
            for (var y = 0; y < Height >> mip; y++)
                bytes.AsSpan((int)(offsets[index] + (ulong)(y * pitches[index])), rowBytes).CopyTo(actual.AsSpan(y * rowBytes));
            Assert.True(expected[index].AsSpan().SequenceEqual(actual), $"layer {index / Mips} mip {mip} differs");
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
