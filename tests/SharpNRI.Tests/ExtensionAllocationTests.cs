// The extensions' per-frame paths allocate nothing on the managed heap, measured the way
// AllocationTests measures the core recording path: on NONE (which does nothing per call, so any
// managed allocation in the loop is the binding's), after a warm-up, over many iterations.
// Covered: MeshShader draws, DescriptorHeap writes and binding, Streamer uploads (single and
// multi-chunk), Upscaler dispatch, Imgui copy and draw, Video decode with a neutral H.264 picture
// desc (marshalled through the per-thread scratch arena), and a low-latency submit.

using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class ExtensionAllocationTests
{
    private const int Iterations = 300;

    [Fact]
    public void Extension_frame_paths_allocate_nothing_on_the_managed_heap()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var commandAllocator = queue.CreateCommandAllocator();
        using var cmd = commandAllocator.CreateCommandBuffer();

        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.ARGUMENT });
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc { type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM, width = 4, height = 4 });
        using var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var heap = device.DescriptorHeap.CreateDescriptorHeap(16, 4);
        using var streamer = device.Streamer.CreateStreamer(new StreamerDesc { constantBufferMemoryLocation = MemoryLocation.HOST_UPLOAD, dynamicBufferMemoryLocation = MemoryLocation.HOST_UPLOAD, queuedFrameNum = 1, hostDataCapacity = 4096 });
        using var upscaler = device.Upscaler.CreateUpscaler(new NriUpscalerDesc { UpscaleResolution = new Dim2_t { w = 8, h = 8 }, Type = UpscalerType.NIS });
        using var imgui = device.Imgui.CreateImgui();
        using var session = device.Video.CreateVideoSession(new VideoSessionDesc { type = VideoSessionType.DECODE, codec = VideoCodec.H264, format = Format.NV12_UNORM, width = 64, height = 64 });
        using var parameters = device.Video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc { Session = session });
        using var picture = device.Video.CreateVideoPicture(new NriVideoPictureDesc { Texture = texture, Usage = VideoPictureUsage.DECODE_OUTPUT });
        using var swapChain = device.SwapChain.CreateSwapChain(new NriSwapChainDesc { Window = new NriWindow { Windows = new NriWindowsWindow(0x1234) }, Queue = queue, Width = 8, Height = 8, TextureNum = 2 });

        // Everything passed by span or memory is built once, here.
        NriWriteResourceDescriptorsDesc[] resourceWrites = [new(view, 0), new(view, 1)];
        NriWriteSamplerDescriptorsDesc[] samplerWrites = [new(sampler, 0)];
        ReadOnlyMemory<byte>[] chunks = [new byte[64], new byte[32]];
        var single = new byte[16];
        var dispatch = new NriDispatchUpscaleDesc { Input = new NriUpscalerResource(texture, view), Output = new NriUpscalerResource(texture, view), CurrentResolution = new Dim2_t { w = 4, h = 4 } };
        ReadOnlyMemory<uint> sliceOffsets = new uint[] { 0, 128 };
        ReadOnlyMemory<NriVideoReference> references = new[] { new NriVideoReference { Picture = picture, Slot = 1 } };
        ReadOnlyMemory<VideoH264DecodeReferenceDesc> h264References = new[] { new VideoH264DecodeReferenceDesc { slot = 1 } };
        ReadOnlyMemory<byte> bitstream = new byte[256];
        var decode = new NriVideoDecodeDesc
        {
            Session = session, Parameters = parameters, DstPicture = picture, References = references,
            Bitstream = new NriVideoDecodeBitstreamRange { Data = bitstream, Size = 256 },
            H264PictureDesc = new NriVideoH264DecodePictureDesc { SliceOffsets = sliceOffsets, References = h264References },
        };
        NriCommandBuffer[] commandBuffers = [];

        void Frame()
        {
            cmd.Begin();
            device.MeshShader.DrawMeshTasks(cmd, 1, 1, 1);
            device.MeshShader.DrawMeshTasksIndirect(cmd, buffer, 0, 1, 12);
            device.DescriptorHeap.WriteResourceDescriptors(heap, resourceWrites);
            device.DescriptorHeap.WriteSamplerDescriptors(heap, samplerWrites);
            device.DescriptorHeap.SetDescriptorHeap(cmd, heap);
            var batch = device.Streamer.BeginStreamerCopyBatch(streamer);
            device.Streamer.StreamBufferData(streamer, new NriStreamBufferDataDesc { DataChunks = chunks, CopyBatch = batch, DstBuffer = buffer });
            device.Streamer.StreamBufferData(streamer, single, 16, batch, buffer, 128);
            device.Streamer.StreamConstantData(streamer, single);
            device.Streamer.CopyStreamedData(cmd, streamer, batch);
            device.Upscaler.DispatchUpscale(cmd, upscaler, dispatch);
            var renderData = device.Imgui.CopyImguiData(cmd, streamer, imgui, default);
            device.Imgui.DrawImgui(cmd, renderData, new DrawImguiDesc { displaySize = new Dim2_t { w = 8, h = 8 }, attachmentFormat = Format.RGBA8_UNORM });
            device.Video.DecodeVideo(cmd, decode);
            cmd.End();
            device.Streamer.EndStreamerFrame(streamer);
            queue.Submit(commandBuffers, default, default, swapChain, 1);
            device.LowLatency.SetLatencyMarker(swapChain, 1, LatencyMarker.SIMULATION_START);
        }

        Frame(); // warm-up: JIT, static constructors, the scratch arena's first chunk, the ArrayPool bucket

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++) Frame();
        var delta = GC.GetAllocatedBytesForCurrentThread() - before;

        GpuWork.Record($"NONE: {Iterations} extension frames: {delta} managed bytes");
        Assert.Equal(0, delta);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
