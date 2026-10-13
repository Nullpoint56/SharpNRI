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
