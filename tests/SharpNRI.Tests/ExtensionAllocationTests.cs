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

    /// <summary>
    /// The rest of every extension's per-frame surface, each overload once per frame: streamer
    /// texture, host and typed constant data, encode with its feedback and bitstream writers, ray
    /// tracing builds, copies, size queries and dispatches, low latency, and presentation waits.
    /// </summary>
    [Fact]
    public void Every_other_extension_call_allocates_nothing_on_the_managed_heap()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var commandAllocator = queue.CreateCommandAllocator();
        using var cmd = commandAllocator.CreateCommandBuffer();
        var rt = device.RayTracing;
        var video = device.Video;

        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 4096, usage = BufferUsageBits.ARGUMENT | BufferUsageBits.SCRATCH | BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT });
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc { type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM, width = 4, height = 4 });
        using var streamer = device.Streamer.CreateStreamer(new StreamerDesc { constantBufferMemoryLocation = MemoryLocation.HOST_UPLOAD, constantBufferSize = 4096, dynamicBufferMemoryLocation = MemoryLocation.HOST_UPLOAD, queuedFrameNum = 1, hostDataCapacity = 4096 });
        using var swapChain = device.SwapChain.CreateSwapChain(new NriSwapChainDesc { Window = new NriWindow { Windows = new NriWindowsWindow(0x1234) }, Queue = queue, Width = 8, Height = 8, TextureNum = 2 });
        using var session = video.CreateVideoSession(new VideoSessionDesc { type = VideoSessionType.ENCODE, codec = VideoCodec.H264, format = Format.NV12_UNORM, width = 64, height = 64 });
        using var parameters = video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc { Session = session });
        using var source = video.CreateVideoPicture(new NriVideoPictureDesc { Texture = texture, Usage = VideoPictureUsage.ENCODE_INPUT });
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc());
        using var blas = rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, new NriAccelerationStructureDesc
        {
            Type = AccelerationStructureType.BOTTOM_LEVEL,
            Geometries = [NriBottomLevelGeometry.FromTriangles(new NriBottomLevelTriangles(NriBuffer.HasBuffer, 0, 3, 12, Format.RGB32_SFLOAT))],
        });
        using var tlas = rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, new NriAccelerationStructureDesc { Type = AccelerationStructureType.TOP_LEVEL, InstanceNum = 1 });
        using var micromap = rt.CreateCommittedMicromap(MemoryLocation.DEVICE, new NriMicromapDesc { Usages = [new MicromapUsageDesc { triangleNum = 1, format = MicromapFormat.OPACITY_2_STATE }] });
        using var sizes = device.CreateQueryPool(QueryType.ACCELERATION_STRUCTURE_SIZE, 2);
        using var micromapSizes = device.CreateQueryPool(QueryType.MICROMAP_COMPACTED_SIZE, 1);
        using var pipeline = rt.CreateRayTracingPipeline(new NriRayTracingPipelineDesc
        {
            PipelineLayout = layout,
            Shaders = [new NriShaderDesc(StageBits.RAYGEN_SHADER, new byte[4], "rayGen")], // NONE accepts any bytecode
            ShaderGroups = [new ShaderGroupDesc()],
        });

        // Everything passed by span or memory is built once, here.
        var texels = new byte[64];
        var host = new byte[16];
        var identifiers = new byte[64];
        var annexB = new byte[256];
        var parameterSets = new NriVideoAnnexBParameterSetsDesc { Codec = VideoCodec.H264, H264Sps = VideoExtensionTests.Sps(), H264Pps = new VideoH264PictureParameterSetDesc() };
        var encode = new NriVideoEncodeDesc
        {
            Session = session, Parameters = parameters, SrcPicture = source,
            DstBitstream = new NriVideoBitstreamRange { Buffer = buffer, Size = 1024 },
            PictureDesc = new VideoEncodePictureDesc { frameType = VideoFrameType.IDR },
            H264PictureDesc = new NriVideoH264EncodePictureDesc(),
        };
        var av1Sequence = VideoExtensionTests.Av1Sequence();
        var infoDesc = new NriVideoAV1EncodeDecodeInfoDesc();
        var triangles = new NriBottomLevelTriangles(buffer, 0, 3, 12, Format.RGB32_SFLOAT);
        NriBottomLevelGeometry[] geometries = [NriBottomLevelGeometry.FromTriangles(triangles)];
        NriBuildBottomLevelAccelerationStructure[] blasBuilds = [new(blas, geometries, buffer, 1024)];
        NriBuildTopLevelAccelerationStructure[] tlasBuilds = [new(tlas, 1, buffer, 0, buffer, 2048)];
        NriBuildMicromap[] micromapBuilds = [new(micromap, buffer, 0, buffer, 256, buffer, 3072)];
        NriAccelerationStructure[] structures = [blas, tlas];
        NriMicromap[] micromaps = [micromap];
        var region = new NriStridedBufferRegion(buffer, 0, 64, 64);
        var rays = new NriDispatchRays(region, region, region, default, 4, 4);
        ulong sink = 0;

        void Frame()
        {
            cmd.Begin();
            device.MeshShader.DrawMeshTasks(cmd, new DrawMeshTasksDesc { x = 1, y = 1, z = 1 });

            var batch = device.Streamer.BeginStreamerCopyBatch(streamer);
            device.Streamer.StreamTextureData(streamer, new NriStreamTextureDataDesc { Data = texels, DataRowPitch = 16, DataSlicePitch = 64, CopyBatch = batch, DstTexture = texture, DstRegion = new TextureRegionDesc { width = 4, height = 1, depth = 1 } });
            sink += device.Streamer.StreamConstantData(streamer, in sink);
            sink += (ulong)device.Streamer.StreamHostData(streamer, host).Length;
            sink += device.Streamer.GetStreamerConstantBuffer(streamer)!.DeviceAddress;
            device.Streamer.CopyStreamedData(cmd, streamer, batch);

            var state = video.GetVideoPictureState(source, VideoPictureRole.ENCODE_INPUT);
            video.EncodeVideo(cmd, encode);
            video.ResolveVideoEncodeFeedback(cmd, session, buffer, 0);
            var feedback = video.GetVideoEncodeFeedback(session, buffer, 0);
            var info = new VideoAV1EncodeDecodeInfo();
            video.GetVideoAV1EncodeDecodeInfo(session, buffer, 0, infoDesc, ref info);
            video.ResetVideoSession(session);
            sink += video.WriteVideoAnnexBParameterSets(parameterSets, annexB) + video.WriteVideoAnnexBEndOfStream(VideoCodec.H264, annexB)
                + video.WriteVideoAV1ObuHeaders(av1Sequence, annexB) + feedback.encodedBitstreamWrittenBytes + (ulong)state.required.layout;

            rt.BuildBottomLevelAccelerationStructures(cmd, blasBuilds);
            rt.BuildBottomLevelAccelerationStructure(cmd, blasBuilds[0]);
            rt.BuildTopLevelAccelerationStructures(cmd, tlasBuilds);
            rt.BuildTopLevelAccelerationStructure(cmd, tlasBuilds[0]);
            rt.BuildMicromaps(cmd, micromapBuilds);
            rt.WriteAccelerationStructureSizes(cmd, structures, sizes, 0);
            rt.WriteMicromapSizes(cmd, micromaps, micromapSizes, 0);
            rt.CopyAccelerationStructure(cmd, blas, blas, CopyMode.CLONE);
            rt.CopyMicromap(cmd, micromap, micromap, CopyMode.CLONE);
            rt.WriteShaderGroupIdentifiers(pipeline, 0, 1, 32, identifiers);
            rt.DispatchRays(cmd, rays);
            device.RayTracing.DispatchRaysIndirect(cmd, buffer, 0);
            sink += (ulong)NriBottomLevelGeometry.FromAabbs(new NriBottomLevelAabbs(buffer, 0, 1, 32)).Type + (ulong)NriBottomLevelGeometry.FromTriangles(triangles).Type;
            sink += blas.GpuHandle + blas.BuildScratchBufferSize + blas.UpdateScratchBufferSize + blas.GetNativeObject() + blas.Buffer.DeviceAddress
                + micromap.BuildScratchBufferSize + micromap.GetNativeObject() + micromap.Buffer.DeviceAddress;
            cmd.End();

            device.LowLatency.SetLatencySleepMode(swapChain, new LatencySleepMode { minIntervalUs = 0 });
            device.LowLatency.SetLatencySleepMode(swapChain, 0, true, false);
            device.LowLatency.LatencySleep(swapChain, 1);
            sink += device.LowLatency.GetLatencyReport(swapChain).inputSampleTimeUs;
            sink += (ulong)device.SwapChain.GetSwapChainTextures(swapChain).Count + (ulong)swapChain.Textures.Count;
            device.SwapChain.WaitForPresent(swapChain, 1);
            device.Streamer.EndStreamerFrame(streamer);
        }

        Frame(); // warm-up: JIT, static constructors, the scratch arena's first chunk, the cached buffer wrappers

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++) Frame();
        var delta = GC.GetAllocatedBytesForCurrentThread() - before;

        GpuWork.Record($"NONE: {Iterations} frames of the other extension calls: {delta} managed bytes (sink {sink})");
        Assert.Equal(0, delta);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
