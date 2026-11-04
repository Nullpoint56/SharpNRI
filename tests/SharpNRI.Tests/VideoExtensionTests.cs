// The Video extension. Acquisition must follow features.video on every backend (UNSUPPORTED is a
// false from TryAcquire, never an exception). The bitstream serializers are host-only code shared by
// every backend (Source/Shared/SharedVideo.h), so where the extension exists they are exercised for
// real, through the managed parameter-set forms, on the GPU backends. NONE takes part only in the
// acquisition test, whose answer is part of the availability table.

using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

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
        // Measured on the development machine's GPU: the Vulkan encode records and submits without a validation
        // error, but its feedback query never becomes available: the resolve's
        // vkCmdCopyQueryPoolResults with WAIT never completes (NRI's fence wait times out), the
        // feedback reads 0 bytes, and the device is left with work pending. Not run until understood.
        Assert.SkipWhen(backend.Api == GraphicsAPI.VK, "VK: the H.264 encode feedback query never becomes available and the resolve hangs (measured, AGENTS.md: Extensions)");

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
        GpuWork.Wait(fence, 1);

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
            GpuWork.Wait(fence, 2);
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

    // ---------------------------------------------------------------- AV1 and H.265

    /// <summary>
    /// Every session of these tests is 256x256 NV12: within the minimum coded extents the Vulkan
    /// drivers report for AV1 and H.265 (decode 128x128 and 144x144, encode 192x128 and 160x64,
    /// measured), and D3D12 reports none (its minimum is the size asked for). Not smaller on D3D12:
    /// asking GetVideoCapabilities for an H.265 decode session at 64x64 removed the device
    /// (DXGI_ERROR_DRIVER_INTERNAL_ERROR, "[233] ID3D12Device::RemoveDevice", measured), inside the
    /// decoder heap NRI creates to answer.
    /// </summary>
    private const ushort Size = 256;

    private static VideoSessionDesc SessionDesc(VideoSessionType type, VideoCodec codec, uint maxReferenceNum = 1) =>
        new() { type = type, codec = codec, format = Format.NV12_UNORM, width = Size, height = Size, maxReferenceNum = maxReferenceNum };

    /// <summary>The capabilities of a session, or a skip naming the session NRI refuses.</summary>
    private static VideoCapabilities RequireSession(Backend backend, NriVideoExtension video, in VideoSessionDesc desc)
    {
        try
        {
            return video.GetVideoCapabilities(desc);
        }
        catch (NriUnsupportedException)
        {
            Assert.Skip($"{backend}: no {desc.codec} {desc.type} session for {desc.width}x{desc.height} NV12 (GetVideoCapabilities: UNSUPPORTED)");
            throw;
        }
    }

    /// <summary>
    /// GetVideoAV1Capabilities for an AV1 decode and an AV1 encode session where the device offers
    /// them (GetVideoCapabilities succeeds; where it does not, the AV1 query is UNSUPPORTED too).
    /// Levels are major * 10 + minor, 2.0 to 7.3. For encode: at least one tile column and row, tile
    /// sizes ordered, superblock sizes among 64 and 128, reference counts and name masks within
    /// AV1's seven reference names, at least one temporal and spatial layer and operating point,
    /// q index range within 0..255, and every required feature also supported. A desc of another
    /// codec is refused by NRI's validation ("'videoSessionDesc' must describe a valid AV1 session").
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void AV1_capabilities_describe_the_decode_and_encode_sessions_the_device_offers(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video (features.video is false)");
        var video = device.Video;

        static bool IsLevel(uint level) => level / 10 is >= 2 and <= 7 && level % 10 <= 3;
        var offered = 0;
        foreach (var type in new[] { VideoSessionType.DECODE, VideoSessionType.ENCODE })
        {
            var desc = SessionDesc(type, VideoCodec.AV1);
            try
            {
                video.GetVideoCapabilities(desc);
            }
            catch (NriUnsupportedException)
            {
                Assert.Throws<NriUnsupportedException>(() => video.GetVideoAV1Capabilities(desc));
                GpuWork.Record($"{backend}: no AV1 {type} session for {Size}x{Size} NV12");
                continue;
            }
            offered++;
            var c = video.GetVideoAV1Capabilities(desc);
            GpuWork.Record($"{backend}: AV1 {type}: level {c.av1MaxLevel}, tiles {c.av1MaxTileColumnNum}x{c.av1MaxTileRowNum}, tile {c.av1MinTileWidth}x{c.av1MinTileHeight}..{c.av1MaxTileWidth}x{c.av1MaxTileHeight}, "
                + $"superblocks 0x{c.av1SuperblockSizeMask:X}, references single {c.av1MaxSingleReferenceNum} (0x{c.av1SingleReferenceNameMask:X}) unidirectional {c.av1MaxUnidirectionalCompoundReferenceNum} (0x{c.av1UnidirectionalCompoundReferenceNameMask:X}) bidirectional {c.av1MaxBidirectionalCompoundReferenceNum} (0x{c.av1BidirectionalCompoundReferenceNameMask:X}), "
                + $"layers {c.av1MaxTemporalLayerNum}/{c.av1MaxSpatialLayerNum}, operating points {c.av1MaxOperatingPointNum}, q index {c.av1MinQIndex}..{c.av1MaxQIndex}, features required {c.av1EncodeRequiredFeatureFlags} supported {c.av1EncodeSupportedFeatureFlags}");
            Assert.True(IsLevel(c.av1MaxLevel), $"AV1 {type}: level {c.av1MaxLevel} is not major * 10 + minor in 2.0..7.3");
            if (type == VideoSessionType.DECODE) continue;

            Assert.True(c.av1MaxTileColumnNum >= 1 && c.av1MaxTileRowNum >= 1, "fewer than one tile column or row");
            Assert.True(c.av1MinTileWidth <= c.av1MaxTileWidth && c.av1MinTileHeight <= c.av1MaxTileHeight && c.av1MaxTileWidth >= 1 && c.av1MaxTileHeight >= 1, "tile size range out of order");
            Assert.True(c.av1SuperblockSizeMask != 0 && (c.av1SuperblockSizeMask & ~3u) == 0, $"superblock size mask 0x{c.av1SuperblockSizeMask:X} names a size other than 64 and 128");
            Assert.True(c.av1MaxSingleReferenceNum is >= 1 and <= 7 && c.av1SingleReferenceNameMask != 0, "a session with a reference allows no single reference");
            foreach (var mask in new[] { c.av1SingleReferenceNameMask, c.av1UnidirectionalCompoundReferenceNameMask, c.av1BidirectionalCompoundReferenceNameMask })
                Assert.Equal(0u, mask & ~0x7Fu);
            Assert.True(c.av1MaxUnidirectionalCompoundReferenceNum <= 7 && c.av1MaxBidirectionalCompoundReferenceNum <= 7, "more compound references than AV1 has names");
            Assert.True(c.av1MaxTemporalLayerNum >= 1 && c.av1MaxSpatialLayerNum >= 1 && c.av1MaxOperatingPointNum >= 1, "no layer or operating point");
            Assert.True(c.av1MinQIndex <= c.av1MaxQIndex && c.av1MaxQIndex <= 255, $"q index range {c.av1MinQIndex}..{c.av1MaxQIndex}");
            Assert.Equal(VideoAV1EncodeFeatureBits.NONE, c.av1EncodeRequiredFeatureFlags & ~c.av1EncodeSupportedFeatureFlags);
        }

        var start = messages.Count;
        var refused = Assert.Throws<NriException>(() => video.GetVideoAV1Capabilities(SessionDesc(VideoSessionType.DECODE, VideoCodec.H264)));
        Assert.Equal(Result.INVALID_ARGUMENT, refused.Result);
        var message = Assert.Single(messages.Skip(start));
        Assert.Contains("'videoSessionDesc' must describe a valid AV1 session", message.Text, StringComparison.Ordinal);
        lock (messages) messages.RemoveAt(start);

        Assert.SkipWhen(offered == 0, $"{backend}: no AV1 decode or encode session for {Size}x{Size} NV12");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// An H.265 session's VPS, SPS and PPS through <see cref="NriVideoH265SessionParametersDesc"/>: NRI's
    /// validation checks the chain the managed arrays describe (each SPS names a VPS present, each PPS
    /// an SPS of the same VPS, ids unique, profile Main for NV12), so a chain that is right is accepted
    /// by NRI and the backend (Vulkan's layer checks the parameter sets it receives), and each break
    /// in it, one field at a time, is refused as "'videoSessionParametersDesc' is invalid for the
    /// fixed session profile, format or picture layout": an SPS naming VPS 1, a PPS naming SPS 1,
    /// two SPS with id 0, a maxSequenceParameterSetNum below the count, an SPS of profile Main 10.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void H265_session_parameters_reach_NRI_through_the_managed_forms(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video (features.video is false)");
        var video = device.Video;
        var desc = SessionDesc(VideoSessionType.DECODE, VideoCodec.H265);
        RequireSession(backend, video, desc);
        using var session = video.CreateVideoSession(desc);

        using (var parameters = video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc { Session = session, H265Parameters = H265Parameters() }))
            Assert.Same(session, parameters.Session);
        Assert.DoesNotContain(messages, m => m.IsError);

        var broken = new (string What, NriVideoH265SessionParametersDesc Parameters)[]
        {
            ("an SPS naming VPS 1", H265Parameters(sps: [H265Sps(vpsId: 1)])),
            ("a PPS naming SPS 1", H265Parameters(pps: [H265Pps(spsId: 1)])),
            ("two SPS with id 0", H265Parameters(sps: [H265Sps(), H265Sps()])),
            ("maxSequenceParameterSetNum 1 for 2 SPS", H265Parameters(sps: [H265Sps(), H265Sps(spsId: 1)], maxSpsNum: 1)),
            ("an SPS of profile Main 10", H265Parameters(sps: [H265Sps(profileIdc: 2)])),
        };
        foreach (var (what, parameters) in broken)
        {
            var start = messages.Count;
            var e = Assert.Throws<NriException>(() => video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc { Session = session, H265Parameters = parameters }).Dispose());
            List<NriMessage> provoked;
            lock (messages) provoked = messages.Skip(start).ToList();
            GpuWork.Record($"{backend}: {what}: {e.Message}");
            Assert.Equal(Result.INVALID_ARGUMENT, e.Result);
            var refusal = Assert.Single(provoked);
            Assert.Contains("'videoSessionParametersDesc' is invalid for the fixed session profile, format or picture layout", refusal.Text, StringComparison.Ordinal);
            lock (messages) messages.RemoveRange(start, messages.Count - start);
        }
        Assert.Equal(1, device.LiveObjects); // the session; nothing refused was created
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// <c>EncodeVideo</c> takes H265ReferenceDescs as "one per entry of references" (NRI reads
    /// referenceNum of them), so a count that differs is refused with ArgumentException before
    /// anything is marshalled or recorded: NRI says nothing and the command buffer closes as it was
    /// begun. Checked on an H.265 encode session where the device offers one.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void EncodeVideo_refuses_H265ReferenceDescs_that_do_not_match_the_references(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var adapter = TestDevice.AdapterFor(backend.Api);
        Assert.SkipWhen(adapter is null || adapter.QueueCount(QueueType.VIDEO_ENCODE) == 0, $"{backend}: the adapter has no video encode queue");
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video (features.video is false)");
        var video = device.Video;
        var encodeQueue = device.GetQueue(QueueType.VIDEO_ENCODE);
        _ = device.GetQueue(QueueType.GRAPHICS);
        var desc = SessionDesc(VideoSessionType.ENCODE, VideoCodec.H265);
        RequireSession(backend, video, desc);

        using var source = device.CreateCommittedTexture(MemoryLocation.DEVICE, VideoTexture(TextureUsageBits.VIDEO_ENCODE, VideoCodec.H265));
        using var reference = device.CreateCommittedTexture(MemoryLocation.DEVICE, VideoTexture(TextureUsageBits.VIDEO_ENCODE | TextureUsageBits.VIDEO_REFERENCE_ONLY, VideoCodec.H265));
        using var bitstream = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 1 << 20, usage = BufferUsageBits.VIDEO_ENCODE });
        using var session = video.CreateVideoSession(desc);
        using var parameters = video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc { Session = session, H265Parameters = H265Parameters() });
        using var sourcePicture = video.CreateVideoPicture(new NriVideoPictureDesc { Texture = source, Usage = VideoPictureUsage.ENCODE_INPUT });
        using var referencePicture = video.CreateVideoPicture(new NriVideoPictureDesc { Texture = reference, Usage = VideoPictureUsage.ENCODE_REFERENCE });

        using var allocator = encodeQueue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        commandBuffer.Begin();
        var start = messages.Count;
        var encode = new NriVideoEncodeDesc
        {
            Session = session,
            Parameters = parameters,
            SrcPicture = sourcePicture,
            DstBitstream = new NriVideoBitstreamRange { Buffer = bitstream, Size = 1 << 20 },
            PictureDesc = new VideoEncodePictureDesc { frameType = VideoFrameType.P, pictureOrderCount = 1 },
            References = new[] { new NriVideoReference { Picture = referencePicture, Slot = 1 } },
            H265ReferenceDescs = new[] { new VideoH265ReferenceDesc { slot = 1 }, new VideoH265ReferenceDesc { slot = 2 } },
        };
        var e = Assert.Throws<ArgumentException>(() => video.EncodeVideo(commandBuffer, encode));
        Assert.Equal("videoEncodeDesc", e.ParamName);
        Assert.Contains("one entry per reference", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => video.EncodeVideo(commandBuffer, encode with { References = ReadOnlyMemory<NriVideoReference>.Empty }));
        Assert.Equal(start, messages.Count);
        commandBuffer.End();
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// An H.265 IDR decode recorded on the video decode queue with the neutral picture desc
    /// (<see cref="NriVideoH265DecodePictureDesc"/>): with the desc right, NRI and the backend's layer
    /// accept the recording and the list closes; nothing is submitted (the bitstream is a start code
    /// and an IDR NAL header, nothing a decoder could decode). Each field group the binding
    /// marshals is then broken once, and NRI's validation, which reads exactly that group, refuses
    /// the decode: slice segment offsets past the bitstream, a PPS id the parameters do not hold, a
    /// reference at the current picture order count, a reference count the picture desc does not
    /// describe.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_H265_decode_records_and_NRI_reads_each_field_of_the_picture_desc(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        using var rig = new DecodeRig(backend, device, VideoCodec.H265, s => new NriVideoSessionParametersDesc { Session = s, H265Parameters = H265Parameters() });

        var idr = new NriVideoH265DecodePictureDesc
        {
            Flags = VideoH265DecodePictureBits.IRAP | VideoH265DecodePictureBits.IDR | VideoH265DecodePictureBits.REFERENCE,
            SliceSegmentOffsets = new uint[] { 0 },
        };
        var decode = rig.Decode with { H265PictureDesc = idr };
        rig.ExpectAccepted(decode, messages);

        var reference = new[] { new NriVideoReference { Picture = rig.ReferencePicture, Slot = 1 } };
        rig.ExpectRefused(decode with { H265PictureDesc = idr with { SliceSegmentOffsets = new uint[] { DecodeRig.BitstreamSize - 4 } } }, messages,
            "'h265PictureDesc->sliceSegmentOffsets' is invalid");
        rig.ExpectRefused(decode with { H265PictureDesc = idr with { PictureParameterSetId = 1 } }, messages,
            "'h265PictureDesc' must select a matching VPS/SPS/PPS chain from 'parameters'");
        rig.ExpectRefused(decode with { References = reference, H265PictureDesc = idr with { References = new[] { new VideoH265ReferenceDesc { slot = 1, pictureOrderCount = 0 } } } }, messages,
            "short-term H.265 references must not have the current picture order count");
        rig.ExpectRefused(decode with { References = reference }, messages,
            "'h265PictureDesc->references' must describe all decode references");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// An AV1 key-frame decode recorded with the neutral picture desc
    /// (<see cref="NriVideoAV1DecodePictureDesc"/>, its tile layout with all four optional arrays,
    /// quantization, loop filter, CDEF, loop restoration and order hints): accepted by NRI and the
    /// backend's layer, the list closes, nothing is submitted. Then each marshalled part is broken
    /// once and NRI's validation, which reads it, refuses the decode as "'av1PictureDesc' is
    /// invalid": no tiles, a tile past the bitstream, a frame header offset past it, a tile layout
    /// of zero columns, a loop restoration whose UV shift exceeds its unit shift, and a reference
    /// whose frame index is 8.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_AV1_decode_records_and_NRI_reads_each_field_of_the_picture_desc(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        using var rig = new DecodeRig(backend, device, VideoCodec.AV1, s => new NriVideoSessionParametersDesc { Session = s, Av1Parameters = new VideoAV1SessionParametersDesc { sequence = Av1Sequence() } });

        const int superblocks = Size / 64;
        var layout = new NriVideoAV1TileLayoutDesc
        {
            ColumnNum = 1,
            RowNum = 1,
            TileSizeBytesMinus1 = 3,
            UniformSpacing = 1,
            MiColumnStarts = new ushort[] { 0, Size / 4 },
            MiRowStarts = new ushort[] { 0, Size / 4 },
            WidthInSuperblocksMinus1 = new ushort[] { superblocks - 1 },
            HeightInSuperblocksMinus1 = new ushort[] { superblocks - 1 },
        };
        var hints = new NriVideoAV1OrderHints();
        for (var i = 0; i < 8; i++) hints[i] = (byte)i;
        var keyFrame = new NriVideoAV1DecodePictureDesc
        {
            FrameType = VideoFrameType.IDR,
            RefreshFrameFlags = 0xFF,
            Flags = VideoAV1PictureBits.SHOW_FRAME,
            RenderWidthMinus1 = Size - 1,
            RenderHeightMinus1 = Size - 1,
            BaseQIndex = 100,
            CodedDenom = 8,
            TxMode = 2,
            TileLayout = layout,
            Quantization = new VideoAV1QuantizationDesc(),
            LoopFilter = new VideoAV1LoopFilterDesc(),
            Cdef = new VideoAV1CdefDesc(),
            LoopRestoration = new VideoAV1LoopRestorationDesc(),
            OrderHints = hints,
            Tiles = new[] { new VideoAV1DecodeTileDesc { offset = 0, size = 64, anchorFrame = 0xFF } },
        };
        var decode = rig.Decode with { Av1PictureDesc = keyFrame };
        rig.ExpectAccepted(decode, messages);

        const string invalid = "'av1PictureDesc' is invalid";
        rig.ExpectRefused(decode with { Av1PictureDesc = keyFrame with { Tiles = ReadOnlyMemory<VideoAV1DecodeTileDesc>.Empty } }, messages, invalid);
        rig.ExpectRefused(decode with { Av1PictureDesc = keyFrame with { Tiles = new[] { new VideoAV1DecodeTileDesc { offset = DecodeRig.BitstreamSize, size = 1 } } } }, messages, invalid);
        rig.ExpectRefused(decode with { Av1PictureDesc = keyFrame with { FrameHeaderOffset = DecodeRig.BitstreamSize } }, messages, invalid);
        rig.ExpectRefused(decode with { Av1PictureDesc = keyFrame with { TileLayout = layout with { ColumnNum = 0 } } }, messages, invalid);
        rig.ExpectRefused(decode with { Av1PictureDesc = keyFrame with { LoopRestoration = new VideoAV1LoopRestorationDesc { lrUnitShift = 0, lrUvShift = 1 } } }, messages, invalid);
        rig.ExpectRefused(decode with { Av1PictureDesc = keyFrame with { References = new[] { new NriVideoAV1ReferenceDesc { RefFrameIndex = 8 } } } }, messages, invalid);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Native decode arguments (<see cref="NriVideoDecodeArgument"/>, DXVA frame arguments) reach
    /// NRI with their type, bytes and count: an argument without bytes is refused as "'arguments[0]'
    /// has invalid data, size, or type" where the session takes native arguments, and with
    /// arguments next to a neutral picture desc, or on a session that takes none (Vulkan), as
    /// "native decode arguments are unsupported or mixed with a neutral picture description".
    /// Eleven arguments are refused by count ("'argumentNum' must be &lt;= 10"). Nothing is
    /// recorded in any of these cases, so the list closes clean.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Native_decode_arguments_reach_NRI_with_their_bytes_and_count(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        using var rig = new DecodeRig(backend, device, VideoCodec.H265, s => new NriVideoSessionParametersDesc { Session = s, H265Parameters = H265Parameters() });
        GpuWork.Record($"{backend}: decodeNativeArgumentsSupported {rig.Caps.decodeNativeArgumentsSupported != 0}");

        var bytes = new NriVideoDecodeArgument { Type = VideoDecodeArgumentType.PICTURE_PARAMETERS, Data = new byte[16] };
        var neutral = new NriVideoH265DecodePictureDesc { Flags = VideoH265DecodePictureBits.IRAP | VideoH265DecodePictureBits.IDR, SliceSegmentOffsets = new uint[] { 0 } };
        const string unsupportedOrMixed = "native decode arguments are unsupported or mixed with a neutral picture description";
        if (rig.Caps.decodeNativeArgumentsSupported != 0)
        {
            rig.ExpectRefused(rig.Decode with { Arguments = new[] { new NriVideoDecodeArgument { Type = VideoDecodeArgumentType.PICTURE_PARAMETERS } } }, messages, "'arguments[0]' has invalid data, size, or type");
            rig.ExpectRefused(rig.Decode with { Arguments = new[] { bytes }, H265PictureDesc = neutral }, messages, unsupportedOrMixed);
        }
        else
            rig.ExpectRefused(rig.Decode with { Arguments = new[] { bytes } }, messages, unsupportedOrMixed);
        rig.ExpectRefused(rig.Decode with { Arguments = Enumerable.Repeat(bytes, 11).ToArray() }, messages, "'argumentNum' must be <= 10");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// AV1 key frames encoded on the video encode queue through NRI's defaults (a null picture desc)
    /// at constant q index 40, and through an explicit <see cref="NriVideoAV1EncodePictureDesc"/>
    /// (NRI's default key-frame flags, a single-tile layout, quantization, loop filter, CDEF, order
    /// hints) at 40 and 160. For each, the feedback reports written bytes without errors, and
    /// GetVideoAV1EncodeDecodeInfo (from the resolved metadata) describes what a decoder needs: a key
    /// frame refreshing every slot, one tile spanning the payload range it reports inside the written
    /// bytes, the session's sequence and render size, and the base q index the encoder used (40,
    /// 160); NRI points the picture's pointers into the info itself. Without a payload header the
    /// call takes the submitted picture descs; with the payload's first bytes passed as the header,
    /// D3D12 answers UNSUPPORTED (its payload is tile data, no OBUs). With a reference snapshot and
    /// a picture desc of frame type IDR it describes a key frame. NRI's D3D12 backend reads the picture
    /// desc's tile layout and segmentation and refuses a 2x1 layout ("D3D12 AV1 encode supports only
    /// a single tile") and explicit segmentation ("... does not support explicit segmentation"). NRI's
    /// validation refuses a reference snapshot whose two entries of one slot disagree on their saved
    /// order hints ("'references' contain inconsistent AV1 identities"), and not the same pair
    /// agreeing.
    /// </summary>
    /// <remarks>
    /// Measured on D3D12 at v190 (master's patches 0032 and 0033): the encoder requires
    /// LOOP_RESTORATION_FILTER, AUTO_SEGMENTATION and CDEF_FILTERING, and NRI enabled automatic
    /// segmentation only for picture flags holding SEGMENTATION_ENABLED, which its default flags do
    /// not (N8); the debug layer refused the encode ("[1306] CVideoEncodeCommandList::EncodeFrame: ...
    /// ENABLE_FRAME_SEGMENTATION_AUTO is required in RequiredFeatureFlags but not set.") and Close
    /// failed. The header path answered FAILURE without a message, and a reference snapshot made NRI
    /// describe the key frame as a P frame (N9). NRI main carries its own fixes for both (af45e72 and
    /// 57d92ff, merged 2026-10-22; 9131c1b for the header): automatic segmentation follows the
    /// encoder's required feature flags, the header is UNSUPPORTED on D3D12, and the frame type is
    /// the submitted picture desc's, a key frame also without references.
    /// The picture desc's base q index is not what the encoder uses under CQP (the rate control's
    /// is), so the q index is set through the rate control.
    /// Vulkan resolves encode feedback through CmdResolveVideoEncodeFeedback, the same
    /// codec-independent feedback query and copy for every codec (CommandBufferVK.hpp, EncodeVideo
    /// and ResolveVideoEncodeFeedback). Measured for H.264, the query never becomes available and the
    /// resolve's vkCmdCopyQueryPoolResults with WAIT never completes, leaving the GPU waiting; that is
    /// not repeated here.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public unsafe void An_AV1_key_frame_encodes_and_NRI_describes_it_for_a_decoder(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var adapter = TestDevice.AdapterFor(backend.Api);
        Assert.SkipWhen(adapter is null || adapter.QueueCount(QueueType.VIDEO_ENCODE) == 0, $"{backend}: the adapter has no video encode queue");
        var messages = new List<NriMessage>();
        using var device = CreateWithVideoQueues(backend.Api, messages);
        Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video (features.video is false)");
        var video = device.Video;
        var desc = SessionDesc(VideoSessionType.ENCODE, VideoCodec.AV1);
        var caps = RequireSession(backend, video, desc);
        Assert.SkipWhen(caps.encodeFeedbackSupported == 0, $"{backend}: AV1 encode without feedback; the written size cannot be measured");
        Assert.SkipWhen(caps.encodeFeedbackResolveRequired != 0, $"{backend}: AV1 encode feedback needs CmdResolveVideoEncodeFeedback, whose query never became available for H.264 (the resolve hangs, measured, AGENTS.md: Extensions)");

        using var encoder = new Av1Encoder(device, video, desc, caps);
        var av1Caps = video.GetVideoAV1Capabilities(desc);
        GpuWork.Record($"{backend}: the AV1 encoder requires {av1Caps.av1EncodeRequiredFeatureFlags}");
        // NRI's default key-frame flags: ERROR_RESILIENT_MODE, DISABLE_CDF_UPDATE, ALLOW_SCREEN_CONTENT_TOOLS,
        // FORCE_INTEGER_MV, SHOW_FRAME, SHOWABLE_FRAME.
        var flags = VideoAV1PictureBits.ERROR_RESILIENT_MODE | VideoAV1PictureBits.DISABLE_CDF_UPDATE | VideoAV1PictureBits.ALLOW_SCREEN_CONTENT_TOOLS
            | VideoAV1PictureBits.FORCE_INTEGER_MV | VideoAV1PictureBits.SHOW_FRAME | VideoAV1PictureBits.SHOWABLE_FRAME;
        var explicitDesc = new NriVideoAV1EncodePictureDesc
        {
            RefreshFrameFlags = 0xFF,
            Flags = flags,
            RenderWidthMinus1 = Size - 1,
            RenderHeightMinus1 = Size - 1,
            CdefDampingMinus3 = 3,
            TileLayout = new NriVideoAV1TileLayoutDesc { ColumnNum = 1, RowNum = 1, TileSizeBytesMinus1 = 3, UniformSpacing = 1 },
            Quantization = new VideoAV1QuantizationDesc(),
            LoopFilter = new VideoAV1LoopFilterDesc(),
            Cdef = new VideoAV1CdefDesc(),
            OrderHints = new NriVideoAV1OrderHints(),
        };

        var cases = new List<(string What, NriVideoAV1EncodePictureDesc? Picture, byte QIndex)>
        {
            ("NRI's defaults, q index 40", null, 40),
            ("picture desc, q index 40", explicitDesc, 40),
            ("picture desc, q index 160", explicitDesc, 160),
        };

        // The picture desc's parts that NRI's D3D12 backend reads and refuses (it encodes one tile and
        // chooses segmentation itself): the refusal shows each reached it. Nothing is encoded then.
        if (backend.Api == GraphicsAPI.D3D12)
        {
            foreach (var (what, picture, refusal) in new[]
            {
                ("a 2x1 tile layout", explicitDesc with { TileLayout = explicitDesc.TileLayout!.Value with { ColumnNum = 2 } }, "D3D12 AV1 encode supports only a single tile"),
                ("explicit segmentation", explicitDesc with { Segmentation = new VideoAV1SegmentationDesc() }, "D3D12 AV1 encode does not support explicit segmentation"),
            })
            {
                var start = messages.Count;
                encoder.Record(40, picture);
                List<NriMessage> provoked;
                lock (messages) provoked = messages.Skip(start).ToList();
                GpuWork.Record($"{backend}: picture desc with {what}: {string.Join(" | ", provoked)}");
                var error = Assert.Single(provoked);
                Assert.True(error.IsError);
                Assert.Contains(refusal, error.Text, StringComparison.Ordinal);
                lock (messages) messages.RemoveRange(start, messages.Count - start);
            }
        }

        var sizes = new List<ulong>();
        foreach (var (what, picture, expectedQIndex) in cases)
        {
            var (feedback, payload) = encoder.Encode(expectedQIndex, picture);
            GpuWork.Record($"{backend}: AV1 key frame, {what}: {feedback.encodedBitstreamWrittenBytes} B at offset {feedback.encodedBitstreamOffset}, average QP {feedback.averageQp}, subregions {feedback.writtenSubregionNum}, errors 0x{feedback.errorFlags:X}, first bytes {Convert.ToHexString(payload.AsSpan(0, Math.Min(16, payload.Length)))}");
            Assert.Equal(0UL, feedback.errorFlags);
            Assert.True(feedback.encodedBitstreamWrittenBytes > 0, "the encode wrote nothing");

            // Without a payload header NRI describes the frame from the metadata and the submitted
            // picture descs (frame type, order hint, refresh flags); NRI's defaults are an empty desc.
            NriVideoAV1EncodeDecodeInfoDesc InfoDesc(VideoFrameType encodedAs, ReadOnlyMemory<NriVideoAV1ReferenceDesc> references = default) => new()
            {
                Feedback = feedback,
                Sequence = Av1Sequence(),
                PictureDesc = new VideoEncodePictureDesc { frameType = encodedAs },
                Av1PictureDesc = picture ?? new NriVideoAV1EncodePictureDesc(),
                References = references,
            };
            var info = new VideoAV1EncodeDecodeInfo();
            video.GetVideoAV1EncodeDecodeInfo(encoder.Session, encoder.Resolved, 0, InfoDesc(VideoFrameType.IDR), ref info);
            var p = &info;
            GpuWork.Record($"{backend}: info: payload {info.bitstreamSize} B at {info.bitstreamOffset}, frame {info.picture.frameType}, refresh 0x{info.picture.refreshFrameFlags:X}, base q index {info.picture.baseQIndex}, tiles {info.picture.tileNum} ({info.tiles[0].offset}+{info.tiles[0].size}), "
                + $"tile layout {info.tileLayout.columnNum}x{info.tileLayout.rowNum}, flags {info.picture.flags}, sequence {info.sequence.maxFrameWidthMinus1 + 1}x{info.sequence.maxFrameHeightMinus1 + 1} ({info.sequence.flags}), CDEF damping-3 {info.picture.cdefDampingMinus3}");
            Assert.Equal(VideoFrameType.IDR, info.picture.frameType);
            Assert.Equal(0xFF, info.picture.refreshFrameFlags);
            Assert.True(info.bitstreamSize > 0 && info.bitstreamOffset + info.bitstreamSize <= feedback.encodedBitstreamWrittenBytes, $"payload range {info.bitstreamOffset}+{info.bitstreamSize} outside the {feedback.encodedBitstreamWrittenBytes} written bytes");
            Assert.Equal(1u, info.picture.tileNum);
            Assert.Equal(info.bitstreamSize, (ulong)info.tiles[0].size);
            Assert.Equal((ushort)(Size - 1), info.sequence.maxFrameWidthMinus1);
            Assert.Equal((ushort)(Size - 1), info.picture.renderWidthMinus1);
            Assert.Equal(expectedQIndex, info.picture.baseQIndex);
            Assert.True(info.picture.tiles == &p->tiles.e0 && info.picture.tileLayout == &p->tileLayout && info.picture.quantization == &p->quantization && info.picture.loopFilter == &p->loopFilter
                && info.picture.cdef == &p->cdef && info.picture.references == &p->references.e0 && info.tileLayout.miColumnStarts == &p->miColumnStarts.e0, "the picture's pointers do not point into the info");
            sizes.Add(feedback.encodedBitstreamWrittenBytes);

            // With the payload's first bytes passed as the header: a D3D12 payload is tile data, with
            // no OBUs to parse, so NRI answers UNSUPPORTED there (9131c1b); Vulkan parses the OBUs.
            {
                var parsed = new VideoAV1EncodeDecodeInfo();
                var withHeader = InfoDesc(VideoFrameType.IDR) with { EncodedPayloadHeader = payload };
                if (backend.Api == GraphicsAPI.D3D12)
                {
                    var unsupported = Assert.Throws<NriUnsupportedException>(() => video.GetVideoAV1EncodeDecodeInfo(encoder.Session, encoder.Resolved, 0, withHeader, ref parsed));
                    GpuWork.Record($"{backend}: info with the payload header ({payload.Length} B) passed: {unsupported.Message}");
                }
                else
                {
                    video.GetVideoAV1EncodeDecodeInfo(encoder.Session, encoder.Resolved, 0, withHeader, ref parsed);
                    GpuWork.Record($"{backend}: info with the payload header ({payload.Length} B) passed: payload {parsed.bitstreamSize} B at {parsed.bitstreamOffset}, frame {parsed.picture.frameType}, base q index {parsed.picture.baseQIndex}");
                    Assert.Equal((info.bitstreamOffset, info.bitstreamSize, info.picture.frameType, info.picture.baseQIndex, info.picture.refreshFrameFlags),
                        (parsed.bitstreamOffset, parsed.bitstreamSize, parsed.picture.frameType, parsed.picture.baseQIndex, parsed.picture.refreshFrameFlags));
                }
            }

            // Reference keys: two entries for slot 0 must agree on everything, saved order hints included.
            var savedHints = new NriVideoAV1OrderHints();
            for (var i = 0; i < 8; i++) savedHints[i] = (byte)(i + 1);
            var otherHints = savedHints;
            otherHints[7] = 0;
            Exception? Describe(NriVideoAV1OrderHints first, NriVideoAV1OrderHints second, out VideoFrameType frameType, VideoFrameType encodedAs = VideoFrameType.IDR)
            {
                var scratch = new VideoAV1EncodeDecodeInfo();
                frameType = VideoFrameType.MAX_NUM;
                try
                {
                    var references = new[] { new NriVideoAV1ReferenceDesc { RefFrameIndex = 0, SavedOrderHints = first }, new NriVideoAV1ReferenceDesc { RefFrameIndex = 0, SavedOrderHints = second } };
                    video.GetVideoAV1EncodeDecodeInfo(encoder.Session, encoder.Resolved, 0, InfoDesc(encodedAs, references), ref scratch);
                    frameType = scratch.picture.frameType;
                    return null;
                }
                catch (Exception e)
                {
                    return e;
                }
            }
            var start = messages.Count;
            var mismatch = Assert.IsType<NriException>(Describe(savedHints, otherHints, out _));
            Assert.Equal(Result.INVALID_ARGUMENT, mismatch.Result);
            Assert.Contains(messages.Skip(start), m => m.IsError && m.Text.Contains("'references' contain inconsistent AV1 identities", StringComparison.Ordinal));
            lock (messages) messages.RemoveRange(start, messages.Count - start);

            // The same references agreeing, with the picture desc's frame type IDR: a key frame.
            var agreeing = Describe(savedHints, savedHints, out var describedAs);
            List<NriMessage> provoked;
            lock (messages) provoked = messages.Skip(start).ToList();
            GpuWork.Record($"{backend}: two agreeing references with the key frame's feedback: {(agreeing is null ? $"described as {describedAs}" : agreeing.Message)}; {string.Join(" | ", provoked)}");
            Assert.DoesNotContain(provoked, m => m.Text.Contains("inconsistent AV1 identities", StringComparison.Ordinal));
            Assert.Null(agreeing);
            Assert.Equal(VideoFrameType.IDR, describedAs);
            lock (messages) messages.RemoveRange(start, messages.Count - start);

            // With frame type P and the same references NRI builds the inter-frame references from
            // the metadata of a key-frame encode: recorded, whatever it answers.
            var asInter = Describe(savedHints, savedHints, out var describedAsInter, VideoFrameType.P);
            lock (messages) provoked = messages.Skip(start).ToList();
            GpuWork.Record($"{backend}: the same references with frame type P: {(asInter is null ? $"described as {describedAsInter}" : asInter.Message)}; {string.Join(" | ", provoked)}");
            lock (messages) messages.RemoveRange(start, messages.Count - start);
        }
        Assert.True(sizes[^1] <= sizes[^2], $"q index 160 wrote {sizes[^1]} B, more than {sizes[^2]} B at 40");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>One AV1 encode session at 256x256 NV12 with one reference, its pictures and buffers, encoding key frames one at a time.</summary>
    private sealed class Av1Encoder : IDisposable
    {
        private readonly List<IDisposable> _owned = [];
        private readonly NriDevice _device;
        private readonly NriVideoExtension _video;
        private readonly NriQueue _queue;
        private readonly NriVideoSessionParameters _parameters;
        private readonly NriTexture _source, _reconstructed;
        private readonly NriVideoPicture _sourcePicture, _reconstructedPicture;
        private readonly NriBuffer _bitstream, _readback;
        private readonly NriBuffer? _metadata;
        private readonly NriFence _fence;
        private readonly ulong _bitstreamSize;
        private ulong _fenceValue;

        public Av1Encoder(NriDevice device, NriVideoExtension video, VideoSessionDesc desc, VideoCapabilities caps)
        {
            _device = device;
            _video = video;
            _queue = device.GetQueue(QueueType.VIDEO_ENCODE);
            _ = device.GetQueue(QueueType.GRAPHICS);
            _source = Own(device.CreateCommittedTexture(MemoryLocation.DEVICE, VideoTexture(TextureUsageBits.VIDEO_ENCODE, VideoCodec.AV1)));
            _reconstructed = Own(device.CreateCommittedTexture(MemoryLocation.DEVICE, VideoTexture(TextureUsageBits.VIDEO_ENCODE | TextureUsageBits.VIDEO_REFERENCE_ONLY, VideoCodec.AV1)
                with { layerNum = (ushort)Math.Max(1u, caps.dpbTextureArrayRequired != 0 ? caps.dpbTextureArrayMinLayerNum : 1) }));
            _bitstreamSize = GpuWork.Align(1 << 20, Math.Max(1u, caps.bitstreamSizeAlignment));
            _bitstream = Own(device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = _bitstreamSize, usage = BufferUsageBits.VIDEO_ENCODE }));
            _readback = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = _bitstreamSize }));
            _metadata = caps.metadataSize == 0 ? null : Own(device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = caps.metadataSize, usage = BufferUsageBits.VIDEO_ENCODE }));
            Resolved = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = Math.Max(caps.resolvedMetadataSize, 256), usage = BufferUsageBits.VIDEO_ENCODE }));
            Session = Own(video.CreateVideoSession(desc));
            _parameters = Own(video.CreateVideoSessionParameters(new NriVideoSessionParametersDesc { Session = Session, Av1Parameters = new VideoAV1SessionParametersDesc { sequence = Av1Sequence() } }));
            _sourcePicture = Own(video.CreateVideoPicture(new NriVideoPictureDesc { Texture = _source, Usage = VideoPictureUsage.ENCODE_INPUT }));
            _reconstructedPicture = Own(video.CreateVideoPicture(new NriVideoPictureDesc { Texture = _reconstructed, Usage = VideoPictureUsage.ENCODE_REFERENCE }));
            _fence = Own(device.CreateFence());
        }

        public NriVideoSession Session { get; }
        public NriBuffer Resolved { get; }

        private T Own<T>(T disposable) where T : IDisposable
        {
            _owned.Add(disposable);
            return disposable;
        }

        /// <summary>Records one key-frame encode and closes the list, without submitting it.</summary>
        public void Record(byte qIndex, NriVideoAV1EncodePictureDesc? picture)
        {
            using var allocator = _queue.CreateCommandAllocator();
            using var commandBuffer = allocator.CreateCommandBuffer();
            RecordEncode(commandBuffer, qIndex, picture);
        }

        /// <summary>Encodes one key frame (the source pixels are left undefined) and returns its feedback and the written bytes.</summary>
        public (VideoEncodeFeedback Feedback, byte[] Payload) Encode(byte qIndex, NriVideoAV1EncodePictureDesc? picture)
        {
            using (var allocator = _queue.CreateCommandAllocator())
            using (var commandBuffer = allocator.CreateCommandBuffer())
            {
                RecordEncode(commandBuffer, qIndex, picture);
                _queue.Submit(commandBuffer, _fence, ++_fenceValue);
                GpuWork.Wait(_fence, _fenceValue);
            }
            var feedback = _video.GetVideoEncodeFeedback(Session, Resolved, 0);

            GpuWork.Run(_device, cmd =>
            {
                cmd.Barrier([], [new NriBufferBarrier(_bitstream, new AccessStage { access = AccessBits.VIDEO_ENCODE_WRITE, stages = StageBits.VIDEO_ENCODE }, GpuWork.CopySource), new NriBufferBarrier(_readback, GpuWork.Nothing, GpuWork.CopyDestination)], []);
                cmd.CopyBuffer(_readback, 0, _bitstream, 0, _bitstreamSize);
            });
            var payload = _readback.Map().Slice((int)feedback.encodedBitstreamOffset, (int)Math.Min(feedback.encodedBitstreamWrittenBytes, _bitstreamSize - feedback.encodedBitstreamOffset)).ToArray();
            _readback.Unmap();
            return (feedback, payload);
        }

        private void RecordEncode(NriCommandBuffer commandBuffer, byte qIndex, NriVideoAV1EncodePictureDesc? picture)
        {
            var inputState = _video.GetVideoPictureState(_sourcePicture, VideoPictureRole.ENCODE_INPUT);
            var reconstructedState = _video.GetVideoPictureState(_reconstructedPicture, VideoPictureRole.ENCODE_RECONSTRUCTED);
            {
                commandBuffer.Begin();
                commandBuffer.Barrier([], [], [
                    new NriTextureBarrier(_source, GpuWork.Undefined, inputState.required),
                    new NriTextureBarrier(_reconstructed, GpuWork.Undefined, reconstructedState.required),
                ]);
                _video.EncodeVideo(commandBuffer, new NriVideoEncodeDesc
                {
                    Session = Session,
                    Parameters = _parameters,
                    SrcPicture = _sourcePicture,
                    DstBitstream = new NriVideoBitstreamRange { Buffer = _bitstream, Size = _bitstreamSize },
                    PictureDesc = new VideoEncodePictureDesc { frameType = VideoFrameType.IDR },
                    RateControlDesc = new VideoEncodeRateControlDesc { mode = VideoEncodeRateControlMode.CQP, qpI = qIndex, qpP = qIndex, qpB = qIndex, frameRateNumerator = 30, frameRateDenominator = 1 },
                    Flags = VideoEncodeBits.FORCE_KEY_FRAME,
                    ReconstructedPicture = _reconstructedPicture,
                    Metadata = _metadata,
                    ResolvedMetadata = Resolved,
                    Av1PictureDesc = picture,
                });
                commandBuffer.End();
            }
        }

        public void Dispose()
        {
            for (var i = _owned.Count - 1; i >= 0; i--) _owned[i].Dispose();
        }
    }

    /// <summary>
    /// A decode session (256x256 NV12, one reference), its parameters, an output picture and a
    /// reference picture, a bitstream buffer holding an Annex-B start code and a NAL header, and a
    /// decode allocator. <see cref="ExpectAccepted"/> and <see cref="ExpectRefused"/> record one
    /// decode each into a fresh command buffer.
    /// </summary>
    private sealed class DecodeRig : IDisposable
    {
        public const uint BitstreamSize = 4096; // a multiple of every bitstream size alignment measured (256)
        private readonly List<IDisposable> _owned = [];
        private readonly NriDevice _device;
        private readonly NriVideoExtension _video;
        private readonly NriCommandAllocator _allocator;
        private readonly NriTexture _output;
        private readonly NriVideoPicture _outputPicture;

        public DecodeRig(Backend backend, NriDevice device, VideoCodec codec, Func<NriVideoSession, NriVideoSessionParametersDesc> parameters)
        {
            _device = device;
            Assert.SkipWhen(device.TryVideo is null, $"{backend}: no video (features.video is false)");
            _video = device.Video;
            var adapter = TestDevice.AdapterFor(backend.Api);
            Assert.SkipWhen(adapter is null || adapter.QueueCount(QueueType.VIDEO_DECODE) == 0, $"{backend}: the adapter has no video decode queue");
            var queue = device.GetQueue(QueueType.VIDEO_DECODE);
            _ = device.GetQueue(QueueType.GRAPHICS);
            var desc = SessionDesc(VideoSessionType.DECODE, codec);
            Caps = RequireSession(backend, _video, desc);
            Assert.SkipWhen(Caps.decodeDpbAndOutputCoincide == 0, $"{backend}: {codec} decode with distinct DPB and output pictures only; these recordings use one picture for both");
            Assert.SkipWhen((Caps.decodeBitstreamSourceMask & VideoDecodeBitstreamSourceBits.BUFFER) == 0, $"{backend}: {codec} decode takes no bitstream buffer");

            _output = Own(device.CreateCommittedTexture(MemoryLocation.DEVICE, VideoTexture(TextureUsageBits.VIDEO_DECODE, codec)));
            var referenceTexture = Own(device.CreateCommittedTexture(MemoryLocation.DEVICE, VideoTexture(TextureUsageBits.VIDEO_DECODE, codec)));
            var bitstream = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = BitstreamSize, usage = BufferUsageBits.VIDEO_DECODE }));
            var bytes = bitstream.Map();
            bytes.Clear();
            ((ReadOnlySpan<byte>)(codec == VideoCodec.H265 ? [0, 0, 0, 1, 19 << 1, 1] : [0x32, 0])).CopyTo(bytes); // an IDR_W_RADL NAL header; an AV1 frame OBU header
            bitstream.Unmap();

            Session = Own(_video.CreateVideoSession(desc));
            Parameters = Own(_video.CreateVideoSessionParameters(parameters(Session)));
            _outputPicture = Own(_video.CreateVideoPicture(new NriVideoPictureDesc { Texture = _output, Usage = VideoPictureUsage.DECODE_OUTPUT }));
            ReferencePicture = Own(_video.CreateVideoPicture(new NriVideoPictureDesc { Texture = referenceTexture, Usage = VideoPictureUsage.DECODE_REFERENCE }));
            _allocator = Own(queue.CreateCommandAllocator());
            Decode = new NriVideoDecodeDesc
            {
                Session = Session,
                Parameters = Parameters,
                Bitstream = new NriVideoDecodeBitstreamRange { Buffer = bitstream, Size = BitstreamSize },
                DstPicture = _outputPicture,
            };
        }

        public VideoCapabilities Caps { get; }
        public NriVideoSession Session { get; }
        public NriVideoSessionParameters Parameters { get; }
        public NriVideoPicture ReferencePicture { get; }

        /// <summary>A decode into the output picture, slot 0, from the whole bitstream buffer; no picture desc yet.</summary>
        public NriVideoDecodeDesc Decode { get; }

        private T Own<T>(T disposable) where T : IDisposable
        {
            _owned.Add(disposable);
            return disposable;
        }

        private (Exception? End, List<NriMessage> Provoked) Record(in NriVideoDecodeDesc decode, List<NriMessage> messages)
        {
            using var commandBuffer = _allocator.CreateCommandBuffer();
            var state = _video.GetVideoPictureState(_outputPicture, VideoPictureRole.DECODE_OUTPUT_AND_SETUP);
            commandBuffer.Begin();
            commandBuffer.Barrier(new NriTextureBarrier(_output, GpuWork.Undefined, state.required));
            var start = messages.Count;
            _video.DecodeVideo(commandBuffer, decode);
            var end = Xunit.Record.Exception(commandBuffer.End);
            List<NriMessage> provoked;
            lock (messages) provoked = messages.Skip(start).ToList();
            _allocator.Reset();
            return (end, provoked);
        }

        /// <summary>The decode is recorded and the list closes, and neither NRI nor the layer reports anything.</summary>
        public void ExpectAccepted(in NriVideoDecodeDesc decode, List<NriMessage> messages)
        {
            var (end, provoked) = Record(decode, messages);
            foreach (var m in provoked) GpuWork.Record($"{_device.GraphicsApi}: accepted decode: {m}");
            Assert.Null(end);
            Assert.DoesNotContain(provoked, m => m.IsError || m.IsWarning);
        }

        /// <summary>NRI's validation refuses the decode with exactly one ERROR containing <paramref name="refusal"/>, which is then dropped from the record.</summary>
        public void ExpectRefused(in NriVideoDecodeDesc decode, List<NriMessage> messages, string refusal)
        {
            var start = messages.Count;
            var (end, provoked) = Record(decode, messages);
            GpuWork.Record($"{_device.GraphicsApi}: refused: {string.Join(" | ", provoked)}");
            Assert.Null(end); // NRI's validation records nothing for a refused decode
            var error = Assert.Single(provoked);
            Assert.True(error.IsError, $"expected NRI validation's ERROR, got {error}");
            Assert.Contains(refusal, error.Text, StringComparison.Ordinal);
            lock (messages) messages.RemoveRange(start, messages.Count - start);
        }

        public void Dispose()
        {
            for (var i = _owned.Count - 1; i >= 0; i--) _owned[i].Dispose();
        }
    }

    private static TextureDesc VideoTexture(TextureUsageBits usage, VideoCodec codec) => new()
    {
        type = TextureType.TEXTURE_2D, usage = usage, format = Format.NV12_UNORM, videoCodec = codec,
        width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
    };

    private static VideoH265ProfileTierLevelDesc H265Main => new() { flags = VideoH265ProfileTierLevelBits.PROGRESSIVE_SOURCE, generalProfileIdc = 1, generalLevelIdc = 93 };

    private static VideoH265VideoParameterSetDesc H265Vps()
    {
        var vps = new VideoH265VideoParameterSetDesc { profileTierLevel = H265Main };
        vps.decPicBufMgr.maxDecPicBufferingMinus1[0] = 1;
        return vps;
    }

    private static NriVideoH265SequenceParameterSetDesc H265Sps(byte vpsId = 0, byte spsId = 0, byte profileIdc = 1)
    {
        var decPicBufMgr = new VideoH265DecPicBufMgrDesc();
        decPicBufMgr.maxDecPicBufferingMinus1[0] = 1;
        return new NriVideoH265SequenceParameterSetDesc
        {
            VideoParameterSetId = vpsId,
            SequenceParameterSetId = spsId,
            ChromaFormatIdc = 1,
            PictureWidthInLumaSamples = Size,
            PictureHeightInLumaSamples = Size,
            Log2MaxPictureOrderCountLsbMinus4 = 4,
            Log2DiffMaxMinLumaCodingBlockSize = 2,
            Log2DiffMaxMinLumaTransformBlockSize = 3,
            MaxTransformHierarchyDepthInter = 1,
            MaxTransformHierarchyDepthIntra = 1,
            ProfileTierLevel = H265Main with { generalProfileIdc = profileIdc },
            DecPicBufMgr = decPicBufMgr,
        };
    }

    private static NriVideoH265PictureParameterSetDesc H265Pps(byte spsId = 0) => new() { SequenceParameterSetId = spsId };

    /// <summary>VPS 0, SPS 0 of VPS 0, PPS 0 of SPS 0 unless given: H.265 Main, 256x256, CTB 32.</summary>
    private static NriVideoH265SessionParametersDesc H265Parameters(NriVideoH265SequenceParameterSetDesc[]? sps = null, NriVideoH265PictureParameterSetDesc[]? pps = null, uint maxSpsNum = 0) => new()
    {
        VideoParameterSets = [H265Vps()],
        SequenceParameterSets = sps ?? [H265Sps()],
        PictureParameterSets = pps ?? [H265Pps()],
        MaxSequenceParameterSetNum = maxSpsNum,
    };

    /// <summary>AV1 Main, 8-bit 4:2:0, 256x256, order hints on.</summary>
    private static VideoAV1SequenceDesc Av1Sequence() => new()
    {
        flags = VideoAV1SequenceBits.ENABLE_ORDER_HINT,
        bitDepth = 8,
        subsamplingX = 1,
        subsamplingY = 1,
        maxFrameWidthMinus1 = Size - 1,
        maxFrameHeightMinus1 = Size - 1,
        frameWidthBitsMinus1 = 7,
        frameHeightBitsMinus1 = 7,
        orderHintBitsMinus1 = 7,
    };

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
