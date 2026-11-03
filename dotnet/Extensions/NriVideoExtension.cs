// L3 — the Video extension (NRIVideo.h).
//
// Backend-neutral hardware video decode and encode: H.264 (High, progressive), H.265 (Main and
// Main10 by format) and AV1 (Main). D3D12 Video on D3D12, VK_KHR_video_* on Vulkan. Available when
// DeviceDesc.features.video is set; UNSUPPORTED otherwise. Three objects, each created and
// destroyed by the extension: a session (one codec, one direction, fixed dimensions), its
// parameters (the codec parameter sets), and pictures (a texture layer a session reads or writes).
//
// NRI's contract, from the header:
//  - decode and encode command buffers come from VIDEO_DECODE or VIDEO_ENCODE queues;
//  - VK: session initialization is implicit on first recorded use, so commands for one session
//    must be recorded in submit order;
//  - ResetVideoSession discards state reserved while recording; reset affected command buffers
//    first;
//  - GetVideoPictureState gives the states a caller-recorded barrier must use around decode and
//    encode;
//  - CmdResolveVideoEncodeFeedback is required only when VideoCapabilities::encodeFeedbackResolveRequired.
// Not thread safe, per NRI ("Threadsafe: no").
//
// Every desc that points at an array or an optional struct has a managed form (descs/NriVideoDescs.cs
// for the pure-data ones, below for the ones naming objects); they are marshalled into per-thread
// scratch memory for the call, so recording allocates nothing in steady state. Decode host
// bitstream data is pinned, not copied.

using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary><c>VideoSessionParametersDesc</c> naming the session and holding the codec parameters by value.</summary>
public sealed class NriVideoSessionParametersDesc
{
    public required NriVideoSession Session { get; init; }

    public NriVideoH264SessionParametersDesc? H264Parameters { get; init; }

    public NriVideoH265SessionParametersDesc? H265Parameters { get; init; }

    public VideoAV1SessionParametersDesc? Av1Parameters { get; init; }
}

/// <summary><c>VideoPictureDesc</c> naming the texture.</summary>
public readonly record struct NriVideoPictureDesc
{
    public required NriTexture Texture { get; init; }

    public VideoPictureUsage Usage { get; init; }

    public ushort Width { get; init; }

    public ushort Height { get; init; }

    public ushort Layer { get; init; }
}

/// <summary><c>VideoReference</c> naming the picture.</summary>
public readonly record struct NriVideoReference
{
    public required NriVideoPicture Picture { get; init; }

    public uint Slot { get; init; }
}

/// <summary><c>VideoBitstreamRange</c> naming the buffer.</summary>
public readonly record struct NriVideoBitstreamRange
{
    public required NriBuffer Buffer { get; init; }

    public ulong Offset { get; init; }

    public ulong Size { get; init; }
}

/// <summary><c>VideoDecodeBitstreamRange</c> naming the buffer, with host data as memory. Provide at least one source supported by <c>VideoCapabilities.decodeBitstreamSourceMask</c>.</summary>
public readonly record struct NriVideoDecodeBitstreamRange
{
    public NriBuffer? Buffer { get; init; }

    /// <summary>Optional host bytes; the range starts at their first byte (<c>offset</c> applies only to <c>buffer</c>). Pinned for the call, not copied.</summary>
    public ReadOnlyMemory<byte> Data { get; init; }

    public ulong Offset { get; init; }

    public ulong Size { get; init; }
}

/// <summary><c>VideoDecodeDesc</c> naming the session, parameters and pictures, with its arrays as memory and the neutral picture descs by value.</summary>
public readonly record struct NriVideoDecodeDesc
{
    public required NriVideoSession Session { get; init; }

    public required NriVideoSessionParameters Parameters { get; init; }

    public NriVideoDecodeBitstreamRange Bitstream { get; init; }

    public required NriVideoPicture DstPicture { get; init; }

    public NriVideoPicture? SetupPicture { get; init; }

    public ReadOnlyMemory<NriVideoReference> References { get; init; }

    public uint DstSlot { get; init; }

    public ReadOnlyMemory<NriVideoDecodeArgument> Arguments { get; init; }

    public NriVideoH264DecodePictureDesc? H264PictureDesc { get; init; }

    public NriVideoH265DecodePictureDesc? H265PictureDesc { get; init; }

    public NriVideoAV1DecodePictureDesc? Av1PictureDesc { get; init; }
}

/// <summary><c>VideoEncodeDesc</c> naming the session, parameters, pictures and buffers, with its arrays as memory and the codec picture descs by value.</summary>
public readonly record struct NriVideoEncodeDesc
{
    public required NriVideoSession Session { get; init; }

    public required NriVideoSessionParameters Parameters { get; init; }

    public required NriVideoPicture SrcPicture { get; init; }

    public NriVideoBitstreamRange DstBitstream { get; init; }

    public ulong BitstreamMetadataSize { get; init; }

    public VideoEncodePictureDesc? PictureDesc { get; init; }

    public VideoEncodeRateControlDesc? RateControlDesc { get; init; }

    public VideoEncodeBits Flags { get; init; }

    public NriVideoPicture? ReconstructedPicture { get; init; }

    public NriBuffer? Metadata { get; init; }

    public ulong MetadataOffset { get; init; }

    public NriBuffer? ResolvedMetadata { get; init; }

    public ulong ResolvedMetadataOffset { get; init; }

    public ReadOnlyMemory<NriVideoReference> References { get; init; }

    public uint ReconstructedSlot { get; init; }

    public NriVideoH264EncodePictureDesc? H264PictureDesc { get; init; }

    public NriVideoAV1EncodePictureDesc? Av1PictureDesc { get; init; }

    /// <summary>H.265 parameter set selection; all ids default to 0.</summary>
    public VideoH265EncodePictureDesc? H265PictureDesc { get; init; }

    /// <summary>Optional, H.265 sessions only: one per entry of <c>references</c> (NRI reads <c>referenceNum</c> of them).</summary>
    public ReadOnlyMemory<VideoH265ReferenceDesc> H265ReferenceDescs { get; init; }
}

/// <summary>A video session: one codec, one direction. Created by <see cref="NriVideoExtension.CreateVideoSession"/>.</summary>
public sealed unsafe class NriVideoSession : NriObject
{
    private readonly NriVideoExtension _extension;

    internal NriVideoSession(NriVideoExtension extension, VideoSession* handle, in VideoSessionDesc desc) : base(handle, "NriVideoSession")
    {
        _extension = extension;
        Desc = desc;
        extension.Device.ChildCreated();
    }

    public override NriDevice Device => _extension.Device;

    /// <summary>The desc the session was created with.</summary>
    public VideoSessionDesc Desc { get; }

    internal VideoSession* Raw => (VideoSession*)Handle;

    private protected override void Destroy(void* handle) => _extension.DestroyVideoSession((VideoSession*)handle);
}

/// <summary>A session's codec parameter sets. Created by <see cref="NriVideoExtension.CreateVideoSessionParameters"/>.</summary>
public sealed unsafe class NriVideoSessionParameters : NriObject
{
    private readonly NriVideoExtension _extension;

    internal NriVideoSessionParameters(NriVideoExtension extension, VideoSessionParameters* handle, NriVideoSession session) : base(handle, "NriVideoSessionParameters")
    {
        _extension = extension;
        Session = session;
        extension.Device.ChildCreated();
    }

    public override NriDevice Device => _extension.Device;

    /// <summary>The session the parameters were created for.</summary>
    public NriVideoSession Session { get; }

    internal VideoSessionParameters* Raw => (VideoSessionParameters*)Handle;

    private protected override void Destroy(void* handle) => _extension.DestroyVideoSessionParameters((VideoSessionParameters*)handle);
}

/// <summary>A texture layer used as a decode or encode picture. Created by <see cref="NriVideoExtension.CreateVideoPicture"/>.</summary>
public sealed unsafe class NriVideoPicture : NriObject
{
    private readonly NriVideoExtension _extension;

    internal NriVideoPicture(NriVideoExtension extension, VideoPicture* handle, NriTexture texture, VideoPictureUsage usage) : base(handle, "NriVideoPicture")
    {
        _extension = extension;
        Texture = texture;
        Usage = usage;
        extension.Device.ChildCreated();
    }

    public override NriDevice Device => _extension.Device;

    /// <summary>The texture the picture refers to; it must outlive the picture.</summary>
    public NriTexture Texture { get; }

    public VideoPictureUsage Usage { get; }

    internal VideoPicture* Raw => (VideoPicture*)Handle;

    private protected override void Destroy(void* handle) => _extension.DestroyVideoPicture((VideoPicture*)handle);
}

/// <summary>NRI's <c>VideoInterface</c>. Acquire through <see cref="NriDevice.Video"/>.</summary>
public sealed unsafe class NriVideoExtension
{
    private readonly VideoInterface _table;

    internal NriVideoExtension(NriDevice device, in VideoInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriVideoExtension? extension)
    {
        if (InterfaceTable.TryAcquire<VideoInterface>(device.Raw, "VideoInterface", out var table))
        {
            extension = new NriVideoExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    // ---------------------------------------------------------------- session

    /// <summary>What the device supports for a session of this type, codec, format and size.</summary>
    public VideoCapabilities GetVideoCapabilities(in VideoSessionDesc videoSessionDesc)
    {
        VideoCapabilities caps;
        fixed (VideoSessionDesc* p = &videoSessionDesc) Device.Check(_table.GetVideoCapabilities(Device.Raw, p, &caps), "GetVideoCapabilities");
        return caps;
    }

    /// <summary>AV1-specific capabilities for an AV1 session desc.</summary>
    public VideoAV1Capabilities GetVideoAV1Capabilities(in VideoSessionDesc videoSessionDesc)
    {
        VideoAV1Capabilities caps;
        fixed (VideoSessionDesc* p = &videoSessionDesc) Device.Check(_table.GetVideoAV1Capabilities(Device.Raw, p, &caps), "GetVideoAV1Capabilities");
        return caps;
    }

    /// <summary>
    /// A session. Fixed profiles: H.264 High (progressive only), H.265 Main/Main10 by format, AV1
    /// Main.
    /// </summary>
    public NriVideoSession CreateVideoSession(in VideoSessionDesc videoSessionDesc)
    {
        VideoSession* handle = null;
        fixed (VideoSessionDesc* p = &videoSessionDesc) Device.Check(_table.CreateVideoSession(Device.Raw, p, &handle), "CreateVideoSession");
        return new NriVideoSession(this, handle, videoSessionDesc);
    }

    /// <summary>
    /// Codec parameter sets for a session. H.264 parameters are required except for decode with
    /// native arguments on a supporting backend; H.265 parameters are required for neutral decode.
    /// </summary>
    public NriVideoSessionParameters CreateVideoSessionParameters(NriVideoSessionParametersDesc videoSessionParametersDesc)
    {
        ArgumentNullException.ThrowIfNull(videoSessionParametersDesc);
        var arena = ScratchArena.Rent();
        try
        {
            var desc = videoSessionParametersDesc.ToNative(arena);
            VideoSessionParameters* handle = null;
            Device.Check(_table.CreateVideoSessionParameters(Device.Raw, &desc, &handle), "CreateVideoSessionParameters");
            return new NriVideoSessionParameters(this, handle, videoSessionParametersDesc.Session);
        }
        finally
        {
            arena.Return();
        }
    }

    /// <summary>A picture over a texture layer. Width and height default to the texture's.</summary>
    public NriVideoPicture CreateVideoPicture(in NriVideoPictureDesc videoPictureDesc)
    {
        ArgumentNullException.ThrowIfNull(videoPictureDesc.Texture);
        var desc = new VideoPictureDesc
        {
            texture = videoPictureDesc.Texture.Raw,
            usage = videoPictureDesc.Usage,
            width = videoPictureDesc.Width,
            height = videoPictureDesc.Height,
            layer = videoPictureDesc.Layer,
        };
        VideoPicture* handle = null;
        Device.Check(_table.CreateVideoPicture(Device.Raw, &desc, &handle), "CreateVideoPicture");
        return new NriVideoPicture(this, handle, videoPictureDesc.Texture, videoPictureDesc.Usage);
    }

    internal void DestroyVideoSession(VideoSession* session) => _table.DestroyVideoSession(session);
    internal void DestroyVideoSessionParameters(VideoSessionParameters* parameters) => _table.DestroyVideoSessionParameters(parameters);
    internal void DestroyVideoPicture(VideoPicture* picture) => _table.DestroyVideoPicture(picture);

    /// <summary>
    /// Discards session state reserved while recording commands. Reset the affected command
    /// buffers first.
    /// </summary>
    public void ResetVideoSession(NriVideoSession videoSession) => _table.ResetVideoSession(videoSession.Raw);

    /// <summary>Backend-specific states for caller-recorded barriers around a decode or encode using the picture in <paramref name="role"/>.</summary>
    public VideoPictureState GetVideoPictureState(NriVideoPicture videoPicture, VideoPictureRole role)
    {
        VideoPictureState state;
        Device.Check(_table.GetVideoPictureState(videoPicture.Raw, role, &state), "GetVideoPictureState");
        return state;
    }

    // ---------------------------------------------------------------- bitstream serialization

    /// <summary>
    /// Serializes H.264 SPS/PPS or H.265 VPS/SPS/PPS to Annex-B bytes in <paramref name="dst"/>.
    /// With an empty <paramref name="dst"/>, only queries the size. Returns <c>writtenSize</c>.
    /// </summary>
    public ulong WriteVideoAnnexBParameterSets(NriVideoAnnexBParameterSetsDesc annexBParameterSetsDesc, Span<byte> dst = default)
    {
        ArgumentNullException.ThrowIfNull(annexBParameterSetsDesc);
        var arena = ScratchArena.Rent();
        try
        {
            var desc = annexBParameterSetsDesc.ToNative(arena);
            fixed (byte* p = dst)
            {
                desc.dst = p;
                desc.dstSize = (ulong)dst.Length;
                Device.Check(_table.WriteVideoAnnexBParameterSets(&desc), "WriteVideoAnnexBParameterSets");
            }
            return desc.writtenSize;
        }
        finally
        {
            arena.Return();
        }
    }

    /// <summary>
    /// Serializes H.264 end-of-sequence/end-of-stream or H.265 EOS/EOB Annex-B NAL units into
    /// <paramref name="dst"/>. With an empty <paramref name="dst"/>, only queries the size. Returns <c>writtenSize</c>.
    /// </summary>
    public ulong WriteVideoAnnexBEndOfStream(VideoCodec codec, Span<byte> dst = default)
    {
        fixed (byte* p = dst)
        {
            var desc = new VideoAnnexBEndOfStreamDesc { codec = codec, dst = p, dstSize = (ulong)dst.Length };
            Device.Check(_table.WriteVideoAnnexBEndOfStream(&desc), "WriteVideoAnnexBEndOfStream");
            return desc.writtenSize;
        }
    }

    /// <summary>
    /// Serializes the AV1 temporal delimiter and sequence header OBUs into <paramref name="dst"/>.
    /// With an empty <paramref name="dst"/>, only queries the size. Returns <c>writtenSize</c>.
    /// </summary>
    public ulong WriteVideoAV1ObuHeaders(in VideoAV1SequenceDesc sequence, Span<byte> dst = default)
    {
        fixed (byte* p = dst)
        {
            var desc = new VideoAV1ObuHeadersDesc { sequence = sequence, dst = p, dstSize = (ulong)dst.Length };
            Device.Check(_table.WriteVideoAV1ObuHeaders(&desc), "WriteVideoAV1ObuHeaders");
            return desc.writtenSize;
        }
    }

    // ---------------------------------------------------------------- command buffer

    /// <summary>
    /// <c>CmdDecodeVideo</c>, on a VIDEO_DECODE command buffer. With no native arguments, the
    /// neutral picture desc of the session's codec is required. No heap allocation in steady state.
    /// </summary>
    public void DecodeVideo(NriCommandBuffer commandBuffer, in NriVideoDecodeDesc videoDecodeDesc)
    {
        var arena = ScratchArena.Rent();
        try
        {
            var desc = videoDecodeDesc.ToNative(arena);
            var bitstream = videoDecodeDesc.Bitstream;
            fixed (byte* data = bitstream.Data.Span)
            {
                desc.bitstream = new VideoDecodeBitstreamRange
                {
                    buffer = bitstream.Buffer is null ? null : bitstream.Buffer.Raw,
                    data = data,
                    offset = bitstream.Offset,
                    size = bitstream.Size,
                };
                _table.CmdDecodeVideo(commandBuffer.Raw, &desc);
            }
        }
        finally
        {
            arena.Return();
        }
    }

    /// <summary><c>CmdEncodeVideo</c>, on a VIDEO_ENCODE command buffer. No heap allocation in steady state.</summary>
    public void EncodeVideo(NriCommandBuffer commandBuffer, in NriVideoEncodeDesc videoEncodeDesc)
    {
        if (!videoEncodeDesc.H265ReferenceDescs.IsEmpty && videoEncodeDesc.H265ReferenceDescs.Length != videoEncodeDesc.References.Length)
            throw new ArgumentException("H265ReferenceDescs must have one entry per reference", nameof(videoEncodeDesc));

        var arena = ScratchArena.Rent();
        try
        {
            var desc = videoEncodeDesc.ToNative(arena);
            _table.CmdEncodeVideo(commandBuffer.Raw, &desc);
        }
        finally
        {
            arena.Return();
        }
    }

    /// <summary>
    /// <c>CmdResolveVideoEncodeFeedback</c>. Required only when
    /// <c>VideoCapabilities.encodeFeedbackResolveRequired</c>; uses the advertised queue and
    /// buffer state.
    /// </summary>
    public void ResolveVideoEncodeFeedback(NriCommandBuffer commandBuffer, NriVideoSession videoSession, NriBuffer resolvedMetadata, ulong resolvedMetadataOffset) =>
        _table.CmdResolveVideoEncodeFeedback(commandBuffer.Raw, videoSession.Raw, resolvedMetadata.Raw, resolvedMetadataOffset);

    /// <summary>Reads the feedback of a finished encode from the resolved metadata in a readback buffer.</summary>
    public VideoEncodeFeedback GetVideoEncodeFeedback(NriVideoSession videoSession, NriBuffer resolvedMetadataReadback, ulong resolvedMetadataOffset)
    {
        VideoEncodeFeedback feedback;
        Device.Check(_table.GetVideoEncodeFeedback(videoSession.Raw, resolvedMetadataReadback.Raw, resolvedMetadataOffset, &feedback), "GetVideoEncodeFeedback");
        return feedback;
    }

    /// <summary>
    /// Fills <paramref name="info"/> with what a decoder needs to decode a finished AV1 encode.
    /// NRI points the pointer fields of <c>info.picture</c> (tile layout, quantization, ...,
    /// tiles, references) into <paramref name="info"/> itself, so it must stay at a fixed address
    /// (a local, or pinned or native memory) for as long as those pointers are used; a copy keeps
    /// pointing at the original.
    /// </summary>
    public void GetVideoAV1EncodeDecodeInfo(NriVideoSession videoSession, NriBuffer resolvedMetadataReadback, ulong resolvedMetadataOffset, in NriVideoAV1EncodeDecodeInfoDesc desc, ref VideoAV1EncodeDecodeInfo info)
    {
        var arena = ScratchArena.Rent();
        try
        {
            var native = desc.ToNative(arena);
            fixed (VideoAV1EncodeDecodeInfo* p = &info)
            {
                Device.Check(_table.GetVideoAV1EncodeDecodeInfo(videoSession.Raw, resolvedMetadataReadback.Raw, resolvedMetadataOffset, &native, p), "GetVideoAV1EncodeDecodeInfo");
            }
        }
        finally
        {
            arena.Return();
        }
    }
}

/// <summary>Marshalling of the managed video forms into scratch memory; see <see cref="ScratchArena"/>.</summary>
internal static unsafe class NriVideoMarshal
{
    internal static VideoH264SessionParametersDesc ToNative(this NriVideoH264SessionParametersDesc s, ScratchArena arena)
    {
        var n = new VideoH264SessionParametersDesc();
        n.sequenceParameterSets = arena.Copy<VideoH264SequenceParameterSetDesc>(s.SequenceParameterSets); n.sequenceParameterSetNum = checked((uint)s.SequenceParameterSets.Length);
        n.pictureParameterSets = arena.Copy<VideoH264PictureParameterSetDesc>(s.PictureParameterSets); n.pictureParameterSetNum = checked((uint)s.PictureParameterSets.Length);
        n.maxSequenceParameterSetNum = s.MaxSequenceParameterSetNum;
        n.maxPictureParameterSetNum = s.MaxPictureParameterSetNum;
        return n;
    }

    internal static VideoH265SequenceParameterSetDesc ToNative(this NriVideoH265SequenceParameterSetDesc s, ScratchArena arena)
    {
        var n = new VideoH265SequenceParameterSetDesc();
        n.flags = s.Flags;
        n.videoParameterSetId = s.VideoParameterSetId;
        n.maxSubLayersMinus1 = s.MaxSubLayersMinus1;
        n.sequenceParameterSetId = s.SequenceParameterSetId;
        n.chromaFormatIdc = s.ChromaFormatIdc;
        n.pictureWidthInLumaSamples = s.PictureWidthInLumaSamples;
        n.pictureHeightInLumaSamples = s.PictureHeightInLumaSamples;
        n.bitDepthLumaMinus8 = s.BitDepthLumaMinus8;
        n.bitDepthChromaMinus8 = s.BitDepthChromaMinus8;
        n.log2MaxPictureOrderCountLsbMinus4 = s.Log2MaxPictureOrderCountLsbMinus4;
        n.log2MinLumaCodingBlockSizeMinus3 = s.Log2MinLumaCodingBlockSizeMinus3;
        n.log2DiffMaxMinLumaCodingBlockSize = s.Log2DiffMaxMinLumaCodingBlockSize;
        n.log2MinLumaTransformBlockSizeMinus2 = s.Log2MinLumaTransformBlockSizeMinus2;
        n.log2DiffMaxMinLumaTransformBlockSize = s.Log2DiffMaxMinLumaTransformBlockSize;
        n.maxTransformHierarchyDepthInter = s.MaxTransformHierarchyDepthInter;
        n.maxTransformHierarchyDepthIntra = s.MaxTransformHierarchyDepthIntra;
        n.pcmSampleBitDepthLumaMinus1 = s.PcmSampleBitDepthLumaMinus1;
        n.pcmSampleBitDepthChromaMinus1 = s.PcmSampleBitDepthChromaMinus1;
        n.log2MinPcmLumaCodingBlockSizeMinus3 = s.Log2MinPcmLumaCodingBlockSizeMinus3;
        n.log2DiffMaxMinPcmLumaCodingBlockSize = s.Log2DiffMaxMinPcmLumaCodingBlockSize;
        n.confWinLeftOffset = s.ConfWinLeftOffset;
        n.confWinRightOffset = s.ConfWinRightOffset;
        n.confWinTopOffset = s.ConfWinTopOffset;
        n.confWinBottomOffset = s.ConfWinBottomOffset;
        n.shortTermRefPicSets = arena.Copy<VideoH265ShortTermRefPicSetDesc>(s.ShortTermRefPicSets); n.numShortTermRefPicSets = checked((byte)s.ShortTermRefPicSets.Length);
        n.numLongTermRefPicsSps = s.NumLongTermRefPicsSps;
        n.profileTierLevel = s.ProfileTierLevel;
        n.decPicBufMgr = s.DecPicBufMgr;
        n.scalingLists = s.ScalingLists is { } scalingListsV ? arena.Push(scalingListsV) : null;
        n.longTermRefPicsSps = s.LongTermRefPicsSps is { } longTermRefPicsSpsV ? arena.Push(longTermRefPicsSpsV) : null;
        return n;
    }

    internal static VideoH265PictureParameterSetDesc ToNative(this NriVideoH265PictureParameterSetDesc s, ScratchArena arena)
    {
        var n = new VideoH265PictureParameterSetDesc();
        n.flags = s.Flags;
        n.pictureParameterSetId = s.PictureParameterSetId;
        n.sequenceParameterSetId = s.SequenceParameterSetId;
        n.videoParameterSetId = s.VideoParameterSetId;
        n.numExtraSliceHeaderBits = s.NumExtraSliceHeaderBits;
        n.refIndexL0DefaultActiveMinus1 = s.RefIndexL0DefaultActiveMinus1;
        n.refIndexL1DefaultActiveMinus1 = s.RefIndexL1DefaultActiveMinus1;
        n.initQpMinus26 = s.InitQpMinus26;
        n.diffCuQpDeltaDepth = s.DiffCuQpDeltaDepth;
        n.cbQpOffset = s.CbQpOffset;
        n.crQpOffset = s.CrQpOffset;
        n.betaOffsetDiv2 = s.BetaOffsetDiv2;
        n.tcOffsetDiv2 = s.TcOffsetDiv2;
        n.log2ParallelMergeLevelMinus2 = s.Log2ParallelMergeLevelMinus2;
        n.tileColumnNumMinus1 = s.TileColumnNumMinus1;
        n.tileRowNumMinus1 = s.TileRowNumMinus1;
        n.columnWidthMinus1 = s.ColumnWidthMinus1;
        n.rowHeightMinus1 = s.RowHeightMinus1;
        n.scalingLists = s.ScalingLists is { } scalingListsV ? arena.Push(scalingListsV) : null;
        return n;
    }

    internal static VideoH265SessionParametersDesc ToNative(this NriVideoH265SessionParametersDesc s, ScratchArena arena)
    {
        var n = new VideoH265SessionParametersDesc();
        n.videoParameterSets = arena.Copy<VideoH265VideoParameterSetDesc>(s.VideoParameterSets); n.videoParameterSetNum = checked((uint)s.VideoParameterSets.Length);
        { var src = s.SequenceParameterSets; var dst = arena.Alloc<VideoH265SequenceParameterSetDesc>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.sequenceParameterSets = dst; n.sequenceParameterSetNum = checked((uint)src.Length); }
        { var src = s.PictureParameterSets; var dst = arena.Alloc<VideoH265PictureParameterSetDesc>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.pictureParameterSets = dst; n.pictureParameterSetNum = checked((uint)src.Length); }
        n.maxVideoParameterSetNum = s.MaxVideoParameterSetNum;
        n.maxSequenceParameterSetNum = s.MaxSequenceParameterSetNum;
        n.maxPictureParameterSetNum = s.MaxPictureParameterSetNum;
        return n;
    }

    internal static VideoAnnexBParameterSetsDesc ToNative(this NriVideoAnnexBParameterSetsDesc s, ScratchArena arena)
    {
        var n = new VideoAnnexBParameterSetsDesc();
        n.codec = s.Codec;
        n.h264Sps = s.H264Sps is { } h264SpsV ? arena.Push(h264SpsV) : null;
        n.h264Pps = s.H264Pps is { } h264PpsV ? arena.Push(h264PpsV) : null;
        n.h265Vps = s.H265Vps is { } h265VpsV ? arena.Push(h265VpsV) : null;
        n.h265Sps = s.H265Sps is { } h265SpsV ? arena.Push(h265SpsV.ToNative(arena)) : null;
        n.h265Pps = s.H265Pps is { } h265PpsV ? arena.Push(h265PpsV.ToNative(arena)) : null;
        return n;
    }

    internal static VideoDecodeArgument ToNative(this NriVideoDecodeArgument s, ScratchArena arena)
    {
        var n = new VideoDecodeArgument();
        n.type = s.Type;
        n.data = (void*)arena.Copy<byte>(s.Data.Span); n.size = checked((uint)s.Data.Span.Length);
        return n;
    }

    internal static VideoH264DecodePictureDesc ToNative(this NriVideoH264DecodePictureDesc s, ScratchArena arena)
    {
        var n = new VideoH264DecodePictureDesc();
        n.flags = s.Flags;
        n.sequenceParameterSetId = s.SequenceParameterSetId;
        n.pictureParameterSetId = s.PictureParameterSetId;
        n.frameNum = s.FrameNum;
        n.idrPictureId = s.IdrPictureId;
        n.topFieldOrderCount = s.TopFieldOrderCount;
        n.bottomFieldOrderCount = s.BottomFieldOrderCount;
        n.sliceOffsets = arena.Copy<uint>(s.SliceOffsets.Span); n.sliceOffsetNum = checked((uint)s.SliceOffsets.Span.Length);
        n.hasReferenceSlot = (byte)(s.HasReferenceSlot ? 1 : 0);
        n.referenceSlot = s.ReferenceSlot;
        n.references = arena.Copy<VideoH264DecodeReferenceDesc>(s.References.Span); n.referenceNum = checked((uint)s.References.Span.Length);
        return n;
    }

    internal static VideoH265DecodePictureDesc ToNative(this NriVideoH265DecodePictureDesc s, ScratchArena arena)
    {
        var n = new VideoH265DecodePictureDesc();
        n.flags = s.Flags;
        n.videoParameterSetId = s.VideoParameterSetId;
        n.sequenceParameterSetId = s.SequenceParameterSetId;
        n.pictureParameterSetId = s.PictureParameterSetId;
        n.pictureOrderCount = s.PictureOrderCount;
        n.numDeltaPocsOfRefRpsIdx = s.NumDeltaPocsOfRefRpsIdx;
        n.numBitsForShortTermRefPicSetInSlice = s.NumBitsForShortTermRefPicSetInSlice;
        n.sliceSegmentOffsets = arena.Copy<uint>(s.SliceSegmentOffsets.Span); n.sliceSegmentOffsetNum = checked((uint)s.SliceSegmentOffsets.Span.Length);
        n.references = arena.Copy<VideoH265ReferenceDesc>(s.References.Span); n.referenceNum = checked((uint)s.References.Span.Length);
        return n;
    }

    internal static VideoH264EncodePictureDesc ToNative(this NriVideoH264EncodePictureDesc s, ScratchArena arena)
    {
        var n = new VideoH264EncodePictureDesc();
        n.sequenceParameterSetId = s.SequenceParameterSetId;
        n.pictureParameterSetId = s.PictureParameterSetId;
        n.references = arena.Copy<VideoH264EncodeReferenceDesc>(s.References.Span); n.referenceNum = checked((uint)s.References.Span.Length);
        return n;
    }

    internal static VideoAV1ReferenceDesc ToNative(this NriVideoAV1ReferenceDesc s, ScratchArena arena)
    {
        var n = new VideoAV1ReferenceDesc();
        n.name = s.Name;
        n.refFrameIndex = s.RefFrameIndex;
        n.frameType = s.FrameType;
        n.orderHint = s.OrderHint;
        n.frameId = s.FrameId;
        n.slot = s.Slot;
        n.savedOrderHints = s.SavedOrderHints is { } savedOrderHintsV ? (byte*)arena.Push(savedOrderHintsV) : null;
        return n;
    }

    internal static VideoAV1TileLayoutDesc ToNative(this NriVideoAV1TileLayoutDesc s, ScratchArena arena)
    {
        var n = new VideoAV1TileLayoutDesc();
        n.columnNum = s.ColumnNum;
        n.rowNum = s.RowNum;
        n.tileSizeBytesMinus1 = s.TileSizeBytesMinus1;
        n.uniformSpacing = s.UniformSpacing;
        n.contextUpdateTileId = s.ContextUpdateTileId;
        n.miColumnStarts = arena.Copy<ushort>(s.MiColumnStarts.Span);
        n.miRowStarts = arena.Copy<ushort>(s.MiRowStarts.Span);
        n.widthInSuperblocksMinus1 = arena.Copy<ushort>(s.WidthInSuperblocksMinus1.Span);
        n.heightInSuperblocksMinus1 = arena.Copy<ushort>(s.HeightInSuperblocksMinus1.Span);
        return n;
    }

    internal static VideoAV1EncodePictureDesc ToNative(this NriVideoAV1EncodePictureDesc s, ScratchArena arena)
    {
        var n = new VideoAV1EncodePictureDesc();
        n.currentFrameId = s.CurrentFrameId;
        n.orderHint = s.OrderHint;
        n.refreshFrameFlags = s.RefreshFrameFlags;
        n.primaryReferenceName = s.PrimaryReferenceName;
        n.flags = s.Flags;
        n.renderWidthMinus1 = s.RenderWidthMinus1;
        n.renderHeightMinus1 = s.RenderHeightMinus1;
        n.codedDenom = s.CodedDenom;
        n.interpolationFilter = s.InterpolationFilter;
        n.txMode = s.TxMode;
        n.baseQIndex = s.BaseQIndex;
        n.cdefDampingMinus3 = s.CdefDampingMinus3;
        n.cdefBits = s.CdefBits;
        n.deltaQRes = s.DeltaQRes;
        n.deltaLfRes = s.DeltaLfRes;
        n.tileLayout = s.TileLayout is { } tileLayoutV ? arena.Push(tileLayoutV.ToNative(arena)) : null;
        n.quantization = s.Quantization is { } quantizationV ? arena.Push(quantizationV) : null;
        n.loopFilter = s.LoopFilter is { } loopFilterV ? arena.Push(loopFilterV) : null;
        n.cdef = s.Cdef is { } cdefV ? arena.Push(cdefV) : null;
        n.segmentation = s.Segmentation is { } segmentationV ? arena.Push(segmentationV) : null;
        n.loopRestoration = s.LoopRestoration is { } loopRestorationV ? arena.Push(loopRestorationV) : null;
        n.globalMotion = s.GlobalMotion is { } globalMotionV ? arena.Push(globalMotionV) : null;
        n.orderHints = s.OrderHints is { } orderHintsV ? (byte*)arena.Push(orderHintsV) : null;
        { var src = s.References.Span; var dst = arena.Alloc<VideoAV1ReferenceDesc>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.references = dst; n.referenceNum = checked((uint)src.Length); }
        return n;
    }

    internal static VideoAV1DecodePictureDesc ToNative(this NriVideoAV1DecodePictureDesc s, ScratchArena arena)
    {
        var n = new VideoAV1DecodePictureDesc();
        n.frameType = s.FrameType;
        n.orderHint = s.OrderHint;
        n.refreshFrameFlags = s.RefreshFrameFlags;
        n.primaryReferenceName = s.PrimaryReferenceName;
        n.currentFrameId = s.CurrentFrameId;
        n.frameHeaderOffset = s.FrameHeaderOffset;
        n.flags = s.Flags;
        n.renderWidthMinus1 = s.RenderWidthMinus1;
        n.renderHeightMinus1 = s.RenderHeightMinus1;
        n.baseQIndex = s.BaseQIndex;
        n.superresDenom = s.SuperresDenom;
        n.codedDenom = s.CodedDenom;
        n.interpolationFilter = s.InterpolationFilter;
        n.txMode = s.TxMode;
        n.cdefDampingMinus3 = s.CdefDampingMinus3;
        n.cdefBits = s.CdefBits;
        n.deltaQRes = s.DeltaQRes;
        n.deltaLfRes = s.DeltaLfRes;
        n.tileLayout = s.TileLayout is { } tileLayoutV ? arena.Push(tileLayoutV.ToNative(arena)) : null;
        n.quantization = s.Quantization is { } quantizationV ? arena.Push(quantizationV) : null;
        n.loopFilter = s.LoopFilter is { } loopFilterV ? arena.Push(loopFilterV) : null;
        n.cdef = s.Cdef is { } cdefV ? arena.Push(cdefV) : null;
        n.segmentation = s.Segmentation is { } segmentationV ? arena.Push(segmentationV) : null;
        n.loopRestoration = s.LoopRestoration is { } loopRestorationV ? arena.Push(loopRestorationV) : null;
        n.globalMotion = s.GlobalMotion is { } globalMotionV ? arena.Push(globalMotionV) : null;
        n.filmGrain = s.FilmGrain is { } filmGrainV ? arena.Push(filmGrainV) : null;
        n.orderHints = s.OrderHints is { } orderHintsV ? (byte*)arena.Push(orderHintsV) : null;
        n.skipModeFrames[0] = s.SkipModeFrame0;
        n.skipModeFrames[1] = s.SkipModeFrame1;
        n.tiles = arena.Copy<VideoAV1DecodeTileDesc>(s.Tiles.Span); n.tileNum = checked((uint)s.Tiles.Span.Length);
        { var src = s.References.Span; var dst = arena.Alloc<VideoAV1ReferenceDesc>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.references = dst; n.referenceNum = checked((uint)src.Length); }
        return n;
    }

    internal static VideoAV1EncodeDecodeInfoDesc ToNative(this NriVideoAV1EncodeDecodeInfoDesc s, ScratchArena arena)
    {
        var n = new VideoAV1EncodeDecodeInfoDesc();
        n.feedback = arena.Push(s.Feedback);
        n.sequence = arena.Push(s.Sequence);
        n.encodedPayloadHeader = arena.Copy<byte>(s.EncodedPayloadHeader.Span); n.encodedPayloadHeaderSize = checked((ulong)s.EncodedPayloadHeader.Span.Length);
        { var src = s.References.Span; var dst = arena.Alloc<VideoAV1ReferenceDesc>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.references = dst; n.referenceNum = checked((uint)src.Length); }
        n.pictureDesc = s.PictureDesc is { } pictureDescV ? arena.Push(pictureDescV) : null;
        n.av1PictureDesc = s.Av1PictureDesc is { } av1PictureDescV ? arena.Push(av1PictureDescV.ToNative(arena)) : null;
        if (!s.SavedFrameStates.IsEmpty && s.SavedFrameStates.Length != 8)
            throw new ArgumentException("SavedFrameStates must be empty or hold 8 entries, one per reference frame slot", nameof(s));
        n.savedFrameStates = s.SavedFrameStates.IsEmpty ? null : arena.Copy<VideoAV1SavedFrameState>(s.SavedFrameStates.Span);
        return n;
    }

    internal static VideoSessionParametersDesc ToNative(this NriVideoSessionParametersDesc s, ScratchArena arena)
    {
        var n = new VideoSessionParametersDesc();
        n.session = s.Session.Raw;
        n.h264Parameters = s.H264Parameters is { } h264ParametersV ? arena.Push(h264ParametersV.ToNative(arena)) : null;
        n.h265Parameters = s.H265Parameters is { } h265ParametersV ? arena.Push(h265ParametersV.ToNative(arena)) : null;
        n.av1Parameters = s.Av1Parameters is { } av1ParametersV ? arena.Push(av1ParametersV) : null;
        return n;
    }

    internal static VideoReference ToNative(this NriVideoReference s, ScratchArena arena)
    {
        var n = new VideoReference();
        n.picture = s.Picture.Raw;
        n.slot = s.Slot;
        return n;
    }

    internal static VideoBitstreamRange ToNative(this NriVideoBitstreamRange s, ScratchArena arena)
    {
        var n = new VideoBitstreamRange();
        n.buffer = s.Buffer.Raw;
        n.offset = s.Offset;
        n.size = s.Size;
        return n;
    }

    internal static VideoDecodeDesc ToNative(this NriVideoDecodeDesc s, ScratchArena arena)
    {
        var n = new VideoDecodeDesc();
        n.session = s.Session.Raw;
        n.parameters = s.Parameters.Raw;
        // bitstream: marshalled by DecodeVideo, inside the fixed that pins the host data (not copied)
        n.dstPicture = s.DstPicture.Raw;
        n.setupPicture = s.SetupPicture is null ? null : s.SetupPicture.Raw;
        { var src = s.References.Span; var dst = arena.Alloc<VideoReference>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.references = dst; n.referenceNum = checked((uint)src.Length); }
        n.dstSlot = s.DstSlot;
        { var src = s.Arguments.Span; var dst = arena.Alloc<VideoDecodeArgument>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.arguments = dst; n.argumentNum = checked((uint)src.Length); }
        n.h264PictureDesc = s.H264PictureDesc is { } h264PictureDescV ? arena.Push(h264PictureDescV.ToNative(arena)) : null;
        n.h265PictureDesc = s.H265PictureDesc is { } h265PictureDescV ? arena.Push(h265PictureDescV.ToNative(arena)) : null;
        n.av1PictureDesc = s.Av1PictureDesc is { } av1PictureDescV ? arena.Push(av1PictureDescV.ToNative(arena)) : null;
        return n;
    }

    internal static VideoEncodeDesc ToNative(this NriVideoEncodeDesc s, ScratchArena arena)
    {
        var n = new VideoEncodeDesc();
        n.session = s.Session.Raw;
        n.parameters = s.Parameters.Raw;
        n.srcPicture = s.SrcPicture.Raw;
        n.dstBitstream = s.DstBitstream.ToNative(arena);
        n.bitstreamMetadataSize = s.BitstreamMetadataSize;
        n.pictureDesc = s.PictureDesc is { } pictureDescV ? arena.Push(pictureDescV) : null;
        n.rateControlDesc = s.RateControlDesc is { } rateControlDescV ? arena.Push(rateControlDescV) : null;
        n.flags = s.Flags;
        n.reconstructedPicture = s.ReconstructedPicture is null ? null : s.ReconstructedPicture.Raw;
        n.metadata = s.Metadata is null ? null : s.Metadata.Raw;
        n.metadataOffset = s.MetadataOffset;
        n.resolvedMetadata = s.ResolvedMetadata is null ? null : s.ResolvedMetadata.Raw;
        n.resolvedMetadataOffset = s.ResolvedMetadataOffset;
        { var src = s.References.Span; var dst = arena.Alloc<VideoReference>(src.Length); for (var i = 0; i < src.Length; i++) dst[i] = src[i].ToNative(arena); n.references = dst; n.referenceNum = checked((uint)src.Length); }
        n.reconstructedSlot = s.ReconstructedSlot;
        n.h264PictureDesc = s.H264PictureDesc is { } h264PictureDescV ? arena.Push(h264PictureDescV.ToNative(arena)) : null;
        n.av1PictureDesc = s.Av1PictureDesc is { } av1PictureDescV ? arena.Push(av1PictureDescV.ToNative(arena)) : null;
        n.h265PictureDesc = s.H265PictureDesc is { } h265PictureDescV ? arena.Push(h265PictureDescV) : null;
        n.h265ReferenceDescs = arena.Copy<VideoH265ReferenceDesc>(s.H265ReferenceDescs.Span);
        return n;
    }
}
