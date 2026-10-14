// The Video extension. Acquisition must follow features.video on every backend (UNSUPPORTED is a
// false from TryAcquire, never an exception). The bitstream serializers are host-only code shared by
// every backend (Source/Shared/SharedVideo.h), so where the extension exists they are exercised for
// real, through the managed parameter-set forms, on the GPU backends. NONE takes part only in the
// acquisition test, whose answer is part of the availability table.

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class VideoExtensionTests
{
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void Acquisition_follows_features_video(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var acquired = device.TryVideo is not null;
        GpuWork.Record($"{backend}: features.video={device.Desc.features.video}, acquired={acquired}");
        Assert.Equal(device.Desc.features.video != 0, acquired);
        if (!acquired) Assert.Throws<NriUnsupportedException>(() => device.Video);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Requesting_video_queues_turns_video_on_where_the_adapter_has_them(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);

        var adapter = TestDevice.AdapterFor(backend.Api);
        var hasVideoQueues = adapter is not null && (adapter.QueueCount(QueueType.VIDEO_DECODE) != 0 || adapter.QueueCount(QueueType.VIDEO_ENCODE) != 0);
        var acquired = device.TryVideo is not null;
        GpuWork.Record($"{backend}: with video queues: features.video={device.Desc.features.video}, acquired={acquired}, adapter video queues={hasVideoQueues}");
        Assert.Equal(device.Desc.features.video != 0, acquired);
        if (backend.Api == GraphicsAPI.D3D12) Assert.Equal(hasVideoQueues, acquired); // D3D12: features.video is exactly "a video queue exists"
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void End_of_stream_NAL_units_serialize(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video");

        var size = device.Video.WriteVideoAnnexBEndOfStream(VideoCodec.H264);
        var bytes = new byte[size];
        Assert.Equal(size, device.Video.WriteVideoAnnexBEndOfStream(VideoCodec.H264, bytes));
        // end_of_seq (type 10) and end_of_stream (type 11), each after a 4-byte start code.
        Assert.Equal(new byte[] { 0, 0, 0, 1, 0x0A, 0x80, 0, 0, 0, 1, 0x0B, 0x80 }, bytes);

        var h265 = new byte[device.Video.WriteVideoAnnexBEndOfStream(VideoCodec.H265)];
        device.Video.WriteVideoAnnexBEndOfStream(VideoCodec.H265, h265);
        // H.265 NAL headers are two bytes: (type << 1, temporal id + 1).
        Assert.Equal(new byte[] { 0, 0, 0, 1, 36 << 1, 1, 0x80, 0, 0, 0, 1, 37 << 1, 1, 0x80 }, h265);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Parameter_sets_serialize_through_the_managed_forms(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video");
        var video = device.Video;

        // H.264: SPS (NAL 0x67) then PPS (NAL 0x68).
        var h264 = new NriVideoAnnexBParameterSetsDesc { Codec = VideoCodec.H264, H264Sps = Sps(), H264Pps = new VideoH264PictureParameterSetDesc() };
        var h264Bytes = new byte[video.WriteVideoAnnexBParameterSets(h264)];
        Assert.Equal((ulong)h264Bytes.Length, video.WriteVideoAnnexBParameterSets(h264, h264Bytes));
        Assert.Equal(new byte[] { 0, 0, 0, 1, 0x67, 100 }, h264Bytes[..6]);
        Assert.True(IndexOf(h264Bytes, [0, 0, 0, 1, 0x68]) > 0, "no PPS NAL unit");

        // H.265: VPS (type 32), SPS (33), PPS (34); the SPS and PPS go through the managed classes.
        var h265 = new NriVideoAnnexBParameterSetsDesc
        {
            Codec = VideoCodec.H265,
            H265Vps = new VideoH265VideoParameterSetDesc { profileTierLevel = new VideoH265ProfileTierLevelDesc { generalProfileIdc = 1, generalLevelIdc = 93 } },
            H265Sps = new NriVideoH265SequenceParameterSetDesc
            {
                ChromaFormatIdc = 1,
                PictureWidthInLumaSamples = 64,
                PictureHeightInLumaSamples = 64,
                Log2DiffMaxMinLumaCodingBlockSize = 1,
                Log2DiffMaxMinLumaTransformBlockSize = 2,
                ProfileTierLevel = new VideoH265ProfileTierLevelDesc { generalProfileIdc = 1, generalLevelIdc = 93 },
            },
            H265Pps = new NriVideoH265PictureParameterSetDesc(),
        };
        var h265Bytes = new byte[video.WriteVideoAnnexBParameterSets(h265)];
        video.WriteVideoAnnexBParameterSets(h265, h265Bytes);
        Assert.Equal(new byte[] { 0, 0, 0, 1, 32 << 1, 1 }, h265Bytes[..6]);
        Assert.True(IndexOf(h265Bytes, [0, 0, 0, 1, 33 << 1, 1]) > 0, "no SPS NAL unit");
        Assert.True(IndexOf(h265Bytes, [0, 0, 0, 1, 34 << 1, 1]) > 0, "no PPS NAL unit");

        // AV1: a temporal delimiter OBU (type 2, empty) leads.
        var sequence = new VideoAV1SequenceDesc { bitDepth = 8, subsamplingX = 1, subsamplingY = 1, maxFrameWidthMinus1 = 63, maxFrameHeightMinus1 = 63, frameWidthBitsMinus1 = 5, frameHeightBitsMinus1 = 5, orderHintBitsMinus1 = 6 };
        var obu = new byte[video.WriteVideoAV1ObuHeaders(sequence)];
        video.WriteVideoAV1ObuHeaders(sequence, obu);
        Assert.Equal(new byte[] { 0x12, 0x00 }, obu[..2]);

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_decode_session_its_parameters_and_a_picture_live_and_die(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video");
        var video = device.Video;

        var sessionDesc = new VideoSessionDesc { type = VideoSessionType.DECODE, codec = VideoCodec.H264, format = Format.NV12_UNORM, width = 64, height = 64 };
        var caps = video.GetVideoCapabilities(sessionDesc);
        GpuWork.Record($"{backend}: H.264 decode 64x64 NV12 caps: min {caps.widthMin}x{caps.heightMin}, max {caps.widthMax}x{caps.heightMax}, maxReferenceNum={caps.maxReferenceNum}");

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.VIDEO_DECODE, format = Format.NV12_UNORM, videoCodec = VideoCodec.H264,
            width = 64, height = 64, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var session = video.CreateVideoSession(sessionDesc);
        using var parameters = video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc
        {
            Session = session,
            H264Parameters = new NriVideoH264SessionParametersDesc { SequenceParameterSets = [Sps()], PictureParameterSets = [new VideoH264PictureParameterSetDesc()] },
        });
        using var picture = video.CreateVideoPicture(new NriVideoPictureDesc { Texture = texture, Usage = VideoPictureUsage.DECODE_OUTPUT });
        Assert.Same(session, parameters.Session);
        Assert.Same(texture, picture.Texture);
        Assert.Equal(4, device.LiveObjects);

        _ = video.GetVideoPictureState(picture, VideoPictureRole.DECODE_OUTPUT);
        video.ResetVideoSession(session);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// One H.264 IDR frame encoded on the video encode queue at constant QP from an NV12 picture
    /// (contents left undefined): the feedback must report written bytes, and the bitstream copied back
    /// on the graphics queue must open with an Annex-B start code and an H.264 NAL unit.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_H264_IDR_frame_encodes_into_the_bitstream_buffer(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var adapter = TestDevice.AdapterFor(backend.Api);
        Assert.SkipWhen(adapter is null || adapter.QueueCount(QueueType.VIDEO_ENCODE) == 0, $"{backend}: the adapter has no video encode queue");
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video (features.video is false)");
        var video = device.Video;
        var encodeQueue = device.GetQueue(QueueType.VIDEO_ENCODE);
        _ = device.GetQueue(QueueType.GRAPHICS); // every queue before any resource, so Vulkan sharing is settled

        var probe = new VideoSessionDesc { type = VideoSessionType.ENCODE, codec = VideoCodec.H264, format = Format.NV12_UNORM, width = 256, height = 256, maxReferenceNum = 1 };
        VideoCapabilities caps;
        try
        {
            caps = video.GetVideoCapabilities(probe);
        }
        catch (NriUnsupportedException)
        {
            Assert.Skip($"{backend}: no H.264 NV12 video encode support");
            return;
        }
        var width = (ushort)GpuWork.Align(Math.Max(256u, caps.widthMin), 16);
        var height = (ushort)GpuWork.Align(Math.Max(256u, caps.heightMin), 16);
        var sessionDesc = probe with { width = width, height = height };
        caps = video.GetVideoCapabilities(sessionDesc);
        GpuWork.Record($"{backend}: H.264 encode {width}x{height}: resolve on {caps.resolvedMetadataQueueType}, min {caps.widthMin}x{caps.heightMin}, bitstream alignment {caps.bitstreamOffsetAlignment}/{caps.bitstreamSizeAlignment}, "
            + $"metadata {caps.metadataSize} B, resolved {caps.resolvedMetadataSize} B, feedback {caps.encodeFeedbackSupported != 0} (resolve {caps.encodeFeedbackResolveRequired != 0}), DPB array {caps.dpbTextureArrayRequired != 0}");
        Assert.SkipWhen(caps.encodeFeedbackSupported == 0, $"{backend}: H.264 encode without feedback; the written size cannot be measured");
        // Measured on the RTX 4070 SUPER: the Vulkan encode records and submits without a validation
        // error, but its feedback query never becomes available: the resolve's
        // vkCmdCopyQueryPoolResults with WAIT never completes (NRI's fence wait times out), the
        // feedback reads 0 bytes, and the device is left with work pending. Not run until understood.
        Assert.SkipWhen(backend.Api == GraphicsAPI.VK, "VK: the H.264 encode feedback query never becomes available and the resolve hangs (measured, CLAUDE.md: Extensions)");

        TextureDesc Picture(TextureUsageBits usage) => new()
        {
            type = TextureType.TEXTURE_2D, usage = usage, format = Format.NV12_UNORM, videoCodec = VideoCodec.H264,
            width = width, height = height, depth = 1, mipNum = 1, layerNum = (ushort)Math.Max(1u, caps.dpbTextureArrayRequired != 0 ? caps.dpbTextureArrayMinLayerNum : 1), sampleNum = 1,
        };
        using var source = device.CreateCommittedTexture(MemoryLocation.DEVICE, Picture(TextureUsageBits.VIDEO_ENCODE) with { layerNum = 1 });
        using var reconstructed = device.CreateCommittedTexture(MemoryLocation.DEVICE, Picture(TextureUsageBits.VIDEO_ENCODE | TextureUsageBits.VIDEO_REFERENCE_ONLY));
        var bitstreamSize = GpuWork.Align(1 << 20, Math.Max(1u, caps.bitstreamSizeAlignment));
        using var bitstream = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = bitstreamSize, usage = BufferUsageBits.VIDEO_ENCODE });
        using var bitstreamReadback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = bitstreamSize });
        using var metadata = caps.metadataSize == 0 ? null : device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = caps.metadataSize, usage = BufferUsageBits.VIDEO_ENCODE });
        var resolvedSize = Math.Max(caps.resolvedMetadataSize, 256);
        using var resolved = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = resolvedSize, usage = BufferUsageBits.VIDEO_ENCODE });

        using var session = video.CreateVideoSession(sessionDesc);
        using var parameters = video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc
        {
            Session = session,
            H264Parameters = new NriVideoH264SessionParametersDesc
            {
                SequenceParameterSets = [Sps() with { pictureWidthInMbsMinus1 = (ushort)(width / 16 - 1), pictureHeightInMapUnitsMinus1 = (ushort)(height / 16 - 1) }],
                PictureParameterSets = [new VideoH264PictureParameterSetDesc()],
            },
        });
        using var sourcePicture = video.CreateVideoPicture(new NriVideoPictureDesc { Texture = source, Usage = VideoPictureUsage.ENCODE_INPUT });
        using var reconstructedPicture = video.CreateVideoPicture(new NriVideoPictureDesc { Texture = reconstructed, Usage = VideoPictureUsage.ENCODE_REFERENCE });

        // The source picture is not filled: NRI's UploadData does not take a two-plane NV12 texture
        // (Vulkan: one copy region with both plane aspects, VUID-VkCopyBufferToImageInfo2-dstImage-07981;
        // D3D12: the second plane's barrier has SyncBefore NONE after an access), and the encode does
        // not depend on what the pixels are.

        var inputState = video.GetVideoPictureState(sourcePicture, VideoPictureRole.ENCODE_INPUT);
        var reconstructedState = video.GetVideoPictureState(reconstructedPicture, VideoPictureRole.ENCODE_RECONSTRUCTED);
        using var allocator = encodeQueue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();
        commandBuffer.Begin();
        commandBuffer.Barrier([], [], [
            new NriTextureBarrier(source, GpuWork.Undefined, inputState.required),
            new NriTextureBarrier(reconstructed, GpuWork.Undefined, reconstructedState.required),
        ]);
        video.EncodeVideo(commandBuffer, new NriVideoEncodeDesc
        {
            Session = session,
            Parameters = parameters,
            SrcPicture = sourcePicture,
            DstBitstream = new NriVideoBitstreamRange { Buffer = bitstream, Offset = 0, Size = bitstreamSize },
            PictureDesc = new VideoEncodePictureDesc { frameType = VideoFrameType.IDR },
            RateControlDesc = new VideoEncodeRateControlDesc { mode = VideoEncodeRateControlMode.CQP, qpI = 26, qpP = 26, qpB = 26, frameRateNumerator = 30, frameRateDenominator = 1 },
            Flags = VideoEncodeBits.FORCE_KEY_FRAME,
            ReconstructedPicture = reconstructedPicture,
            Metadata = metadata,
            ResolvedMetadata = resolved,
            H264PictureDesc = new NriVideoH264EncodePictureDesc(),
        });
        commandBuffer.End();
        encodeQueue.Submit(commandBuffer, fence, 1);
        fence.Wait(1);

        // Where the backend needs the feedback resolved, it says on which queue and in which state.
        if (caps.encodeFeedbackResolveRequired != 0)
        {
            var resolveQueue = device.GetQueue(caps.resolvedMetadataQueueType);
            using var resolveAllocator = resolveQueue.CreateCommandAllocator();
            using var resolveCommands = resolveAllocator.CreateCommandBuffer();
            resolveCommands.Begin();
            resolveCommands.Barrier(new NriBufferBarrier(resolved, GpuWork.Nothing, caps.resolvedMetadataState));
            video.ResolveVideoEncodeFeedback(resolveCommands, session, resolved, 0);
            resolveCommands.End();
            resolveQueue.Submit(resolveCommands, fence, 2);
            fence.Wait(2);
        }
        var feedback = video.GetVideoEncodeFeedback(session, resolved, 0);
        GpuWork.Record($"{backend}: IDR frame: {feedback.encodedBitstreamWrittenBytes} B at offset {feedback.encodedBitstreamOffset}, average QP {feedback.averageQp}, intra units {feedback.intraCodingUnitNum}, errors 0x{feedback.errorFlags:X}");
        Assert.Equal(0UL, feedback.errorFlags);
        Assert.InRange(feedback.encodedBitstreamWrittenBytes, 1UL, bitstreamSize - feedback.encodedBitstreamOffset);

        GpuWork.Run(device, cmd =>
        {
            cmd.Barrier([], [new NriBufferBarrier(bitstream, new AccessStage { access = AccessBits.VIDEO_ENCODE_WRITE, stages = StageBits.VIDEO_ENCODE }, GpuWork.CopySource), new NriBufferBarrier(bitstreamReadback, GpuWork.Nothing, GpuWork.CopyDestination)], []);
            cmd.CopyBuffer(bitstreamReadback, 0, bitstream, 0, bitstreamSize);
        });
        var bytes = bitstreamReadback.Map().Slice((int)feedback.encodedBitstreamOffset, (int)feedback.encodedBitstreamWrittenBytes).ToArray();
        bitstreamReadback.Unmap();
        GpuWork.Record($"{backend}: first bytes {Convert.ToHexString(bytes.AsSpan(0, Math.Min(16, bytes.Length)))}");
        // Annex-B: a start code, then the IDR slice (nal_unit_type 5) or the parameter sets NRI may
        // prepend (7 SPS, 8 PPS). Measured on D3D12: 00 00 00 01 65, the IDR slice first.
        var start = bytes.AsSpan().IndexOf(new byte[] { 0, 0, 1 });
        Assert.True(start >= 0 && start + 3 < bytes.Length, $"no Annex-B start code in {bytes.Length} bytes");
        Assert.Contains(bytes[start + 3] & 0x1F, new[] { 5, 7, 8 });
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static VideoH264SequenceParameterSetDesc Sps() => new()
    {
        profileIdc = 100, // High
        levelIdc = 30,
        chromaFormatIdc = 1,
        log2MaxFrameNumMinus4 = 0,
        log2MaxPictureOrderCountLsbMinus4 = 2,
        referenceFrameNum = 1,
        pictureWidthInMbsMinus1 = 3,
        pictureHeightInMapUnitsMinus1 = 3,
        flags = VideoH264SequenceParameterSetBits.FRAME_MBS_ONLY,
    };

    /// <summary>
    /// A validated device with one video decode and one video encode queue where the adapter has
    /// them: NRI reports features.video only for the queues that were created (queueNum is patched
    /// to the created queues), and the default test device creates a graphics queue only.
    /// </summary>
    private static NriDevice CreateWithVideoQueues(GraphicsAPI api, List<NriMessage> messages) => TestDevice.Create(api, messages, b =>
    {
        var adapter = TestDevice.AdapterFor(api);
        if (adapter is null) return;
        var families = new List<NriQueueFamily> { new(QueueType.GRAPHICS) };
        if (adapter.QueueCount(QueueType.VIDEO_DECODE) != 0) families.Add(new NriQueueFamily(QueueType.VIDEO_DECODE));
        if (adapter.QueueCount(QueueType.VIDEO_ENCODE) != 0) families.Add(new NriQueueFamily(QueueType.VIDEO_ENCODE));
        b.QueueFamilies = families;
    });

    private static int IndexOf(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle);
}
