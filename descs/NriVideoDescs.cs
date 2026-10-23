// Managed forms of the NRIVideo.h descs that hold pointers to arrays or to optional structs and
// name no device objects.
//
// NRI's video descs nest: a session-parameters desc points at arrays of parameter sets, an H.265
// sequence parameter set points at an array of short-term reference picture sets and at optional
// scaling lists, an AV1 picture desc points at a tile layout that points at four arrays. These are
// the same descs with arrays where NRI has pointer-plus-count and nullable values where NRI has an
// optional pointer to one struct; every other field keeps NRI's name (PascalCase) and type, and the
// leaf structs are the generated ones. Creation forms (session parameters) are classes holding
// arrays; per-call forms (decode, encode) are record structs holding ReadOnlyMemory, so a caller
// can slice reusable buffers and nothing is allocated per call. PrismNRI marshals them into
// per-thread scratch memory for the duration of the call; NRI copies what it keeps.
//
// The property set of each form was produced from the generated L0 struct and the header comments
// (field order is NRI's), then reviewed; a field that NRI documents carries its comment.

using System.Runtime.CompilerServices;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>Eight AV1 order hints, inline: the "must include 8 entries" arrays of <c>NRIVideo.h</c>.</summary>
[InlineArray(8)]
public struct NriVideoAV1OrderHints
{
    private byte _e0;
}

/// <summary><c>VideoH264SessionParametersDesc</c> with its parameter sets as arrays.</summary>
public sealed class NriVideoH264SessionParametersDesc
{
    public VideoH264SequenceParameterSetDesc[] SequenceParameterSets { get; init; } = [];

    public VideoH264PictureParameterSetDesc[] PictureParameterSets { get; init; } = [];

    public uint MaxSequenceParameterSetNum { get; init; }

    public uint MaxPictureParameterSetNum { get; init; }
}

/// <summary><c>VideoH265SequenceParameterSetDesc</c> with its short-term reference picture sets as an array and its optional structs by value.</summary>
public sealed class NriVideoH265SequenceParameterSetDesc
{
    public VideoH265SequenceParameterSetBits Flags { get; init; }

    public byte VideoParameterSetId { get; init; }

    public byte MaxSubLayersMinus1 { get; init; }

    public byte SequenceParameterSetId { get; init; }

    public byte ChromaFormatIdc { get; init; }

    public uint PictureWidthInLumaSamples { get; init; }

    public uint PictureHeightInLumaSamples { get; init; }

    public byte BitDepthLumaMinus8 { get; init; }

    public byte BitDepthChromaMinus8 { get; init; }

    public byte Log2MaxPictureOrderCountLsbMinus4 { get; init; }

    public byte Log2MinLumaCodingBlockSizeMinus3 { get; init; }

    public byte Log2DiffMaxMinLumaCodingBlockSize { get; init; }

    public byte Log2MinLumaTransformBlockSizeMinus2 { get; init; }

    public byte Log2DiffMaxMinLumaTransformBlockSize { get; init; }

    public byte MaxTransformHierarchyDepthInter { get; init; }

    public byte MaxTransformHierarchyDepthIntra { get; init; }

    public byte PcmSampleBitDepthLumaMinus1 { get; init; }

    public byte PcmSampleBitDepthChromaMinus1 { get; init; }

    public byte Log2MinPcmLumaCodingBlockSizeMinus3 { get; init; }

    public byte Log2DiffMaxMinPcmLumaCodingBlockSize { get; init; }

    public uint ConfWinLeftOffset { get; init; }

    public uint ConfWinRightOffset { get; init; }

    public uint ConfWinTopOffset { get; init; }

    public uint ConfWinBottomOffset { get; init; }

    public VideoH265ShortTermRefPicSetDesc[] ShortTermRefPicSets { get; init; } = [];

    public byte NumLongTermRefPicsSps { get; init; }

    public VideoH265ProfileTierLevelDesc ProfileTierLevel { get; init; }

    public VideoH265DecPicBufMgrDesc DecPicBufMgr { get; init; }

    public VideoH265ScalingListsDesc? ScalingLists { get; init; }

    public VideoH265LongTermRefPicsSpsDesc? LongTermRefPicsSps { get; init; }
}

/// <summary><c>VideoH265PictureParameterSetDesc</c> with its optional scaling lists by value.</summary>
public sealed class NriVideoH265PictureParameterSetDesc
{
    public VideoH265PictureParameterSetBits Flags { get; init; }

    public byte PictureParameterSetId { get; init; }

    public byte SequenceParameterSetId { get; init; }

    public byte VideoParameterSetId { get; init; }

    public byte NumExtraSliceHeaderBits { get; init; }

    public byte RefIndexL0DefaultActiveMinus1 { get; init; }

    public byte RefIndexL1DefaultActiveMinus1 { get; init; }

    public sbyte InitQpMinus26 { get; init; }

    public byte DiffCuQpDeltaDepth { get; init; }

    public sbyte CbQpOffset { get; init; }

    public sbyte CrQpOffset { get; init; }

    public sbyte BetaOffsetDiv2 { get; init; }

    public sbyte TcOffsetDiv2 { get; init; }

    public byte Log2ParallelMergeLevelMinus2 { get; init; }

    public byte TileColumnNumMinus1 { get; init; }

    public byte TileRowNumMinus1 { get; init; }

    public VideoH265PictureParameterSetDesc._columnWidthMinus1_e__FixedBuffer ColumnWidthMinus1 { get; init; }

    public VideoH265PictureParameterSetDesc._rowHeightMinus1_e__FixedBuffer RowHeightMinus1 { get; init; }

    public VideoH265ScalingListsDesc? ScalingLists { get; init; }
}

/// <summary><c>VideoH265SessionParametersDesc</c> with its parameter sets as arrays.</summary>
public sealed class NriVideoH265SessionParametersDesc
{
    public VideoH265VideoParameterSetDesc[] VideoParameterSets { get; init; } = [];

    public NriVideoH265SequenceParameterSetDesc[] SequenceParameterSets { get; init; } = [];

    public NriVideoH265PictureParameterSetDesc[] PictureParameterSets { get; init; } = [];

    public uint MaxVideoParameterSetNum { get; init; }

    public uint MaxSequenceParameterSetNum { get; init; }

    public uint MaxPictureParameterSetNum { get; init; }
}

/// <summary><c>VideoAnnexBParameterSetsDesc</c> with its parameter sets by value. The destination (<c>dst</c>, <c>dstSize</c>) and <c>writtenSize</c> are the parameter and result of <c>NriVideoExtension.WriteVideoAnnexBParameterSets</c>.</summary>
public sealed class NriVideoAnnexBParameterSetsDesc
{
    public VideoCodec Codec { get; init; }

    public VideoH264SequenceParameterSetDesc? H264Sps { get; init; }

    public VideoH264PictureParameterSetDesc? H264Pps { get; init; }

    public VideoH265VideoParameterSetDesc? H265Vps { get; init; }

    public NriVideoH265SequenceParameterSetDesc? H265Sps { get; init; }

    public NriVideoH265PictureParameterSetDesc? H265Pps { get; init; }
}

/// <summary><c>VideoDecodeArgument</c>: a native DXVA frame argument with its bytes as memory.</summary>
public readonly record struct NriVideoDecodeArgument
{
    public VideoDecodeArgumentType Type { get; init; }

    /// <summary>The bytes of the argument; <c>size</c> is their length.</summary>
    public ReadOnlyMemory<byte> Data { get; init; }
}

/// <summary><c>VideoH264DecodePictureDesc</c> with its slice offsets and references as memory.</summary>
public readonly record struct NriVideoH264DecodePictureDesc
{
    public VideoH264DecodePictureBits Flags { get; init; }

    public byte SequenceParameterSetId { get; init; }

    public byte PictureParameterSetId { get; init; }

    public ushort FrameNum { get; init; }

    public ushort IdrPictureId { get; init; }

    public int TopFieldOrderCount { get; init; }

    public int BottomFieldOrderCount { get; init; }

    public ReadOnlyMemory<uint> SliceOffsets { get; init; }

    public bool HasReferenceSlot { get; init; }

    public uint ReferenceSlot { get; init; }

    public ReadOnlyMemory<VideoH264DecodeReferenceDesc> References { get; init; }
}

/// <summary><c>VideoH265DecodePictureDesc</c> with its slice segment offsets and references as memory.</summary>
public readonly record struct NriVideoH265DecodePictureDesc
{
    public VideoH265DecodePictureBits Flags { get; init; }

    public byte VideoParameterSetId { get; init; }

    public byte SequenceParameterSetId { get; init; }

    public byte PictureParameterSetId { get; init; }

    public int PictureOrderCount { get; init; }

    public byte NumDeltaPocsOfRefRpsIdx { get; init; }

    public ushort NumBitsForShortTermRefPicSetInSlice { get; init; }

    public ReadOnlyMemory<uint> SliceSegmentOffsets { get; init; }

    public ReadOnlyMemory<VideoH265ReferenceDesc> References { get; init; }
}

/// <summary><c>VideoH264EncodePictureDesc</c> with its references as memory.</summary>
public readonly record struct NriVideoH264EncodePictureDesc
{
    public byte SequenceParameterSetId { get; init; }

    public byte PictureParameterSetId { get; init; }

    public ReadOnlyMemory<VideoH264EncodeReferenceDesc> References { get; init; }
}

/// <summary><c>VideoAV1ReferenceDesc</c> with its optional saved order hints inline.</summary>
public readonly record struct NriVideoAV1ReferenceDesc
{
    public VideoAV1ReferenceName Name { get; init; }

    public byte RefFrameIndex { get; init; }

    public VideoFrameType FrameType { get; init; }

    public byte OrderHint { get; init; }

    public uint FrameId { get; init; }

    public uint Slot { get; init; }

    /// <summary>Optional; all 8 entries when provided.</summary>
    public NriVideoAV1OrderHints? SavedOrderHints { get; init; }
}

/// <summary><c>VideoAV1TileLayoutDesc</c> with its optional arrays as memory (empty means not provided).</summary>
public readonly record struct NriVideoAV1TileLayoutDesc
{
    public byte ColumnNum { get; init; }

    public byte RowNum { get; init; }

    public byte TileSizeBytesMinus1 { get; init; }

    public byte UniformSpacing { get; init; }

    public ushort ContextUpdateTileId { get; init; }

    /// <summary>Optional; <c>columnNum + 1</c> entries when provided.</summary>
    public ReadOnlyMemory<ushort> MiColumnStarts { get; init; }

    /// <summary>Optional; <c>rowNum + 1</c> entries when provided.</summary>
    public ReadOnlyMemory<ushort> MiRowStarts { get; init; }

    /// <summary>Optional; <c>columnNum</c> entries when provided.</summary>
    public ReadOnlyMemory<ushort> WidthInSuperblocksMinus1 { get; init; }

    /// <summary>Optional; <c>rowNum</c> entries when provided.</summary>
    public ReadOnlyMemory<ushort> HeightInSuperblocksMinus1 { get; init; }
}

/// <summary><c>VideoAV1EncodePictureDesc</c> with its optional structs by value and its references as memory.</summary>
public readonly record struct NriVideoAV1EncodePictureDesc
{
    public uint CurrentFrameId { get; init; }

    public byte OrderHint { get; init; }

    public byte RefreshFrameFlags { get; init; }

    public VideoAV1ReferenceName PrimaryReferenceName { get; init; }

    public VideoAV1PictureBits Flags { get; init; }

    public ushort RenderWidthMinus1 { get; init; }

    public ushort RenderHeightMinus1 { get; init; }

    public byte CodedDenom { get; init; }

    public byte InterpolationFilter { get; init; }

    public byte TxMode { get; init; }

    public byte BaseQIndex { get; init; }

    public byte CdefDampingMinus3 { get; init; }

    public byte CdefBits { get; init; }

    public byte DeltaQRes { get; init; }

    public byte DeltaLfRes { get; init; }

    public NriVideoAV1TileLayoutDesc? TileLayout { get; init; }

    public VideoAV1QuantizationDesc? Quantization { get; init; }

    public VideoAV1LoopFilterDesc? LoopFilter { get; init; }

    public VideoAV1CdefDesc? Cdef { get; init; }

    public VideoAV1SegmentationDesc? Segmentation { get; init; }

    public VideoAV1LoopRestorationDesc? LoopRestoration { get; init; }

    public VideoAV1GlobalMotionDesc? GlobalMotion { get; init; }

    /// <summary>Optional; all 8 entries when provided.</summary>
    public NriVideoAV1OrderHints? OrderHints { get; init; }

    public ReadOnlyMemory<NriVideoAV1ReferenceDesc> References { get; init; }
}

/// <summary><c>VideoAV1DecodePictureDesc</c> with its optional structs by value and its tiles and references as memory.</summary>
public readonly record struct NriVideoAV1DecodePictureDesc
{
    public VideoFrameType FrameType { get; init; }

    public byte OrderHint { get; init; }

    public byte RefreshFrameFlags { get; init; }

    public VideoAV1ReferenceName PrimaryReferenceName { get; init; }

    public uint CurrentFrameId { get; init; }

    public uint FrameHeaderOffset { get; init; }

    public VideoAV1PictureBits Flags { get; init; }

    public ushort RenderWidthMinus1 { get; init; }

    public ushort RenderHeightMinus1 { get; init; }

    public byte BaseQIndex { get; init; }

    public byte SuperresDenom { get; init; }

    public byte CodedDenom { get; init; }

    public byte InterpolationFilter { get; init; }

    public byte TxMode { get; init; }

    public byte CdefDampingMinus3 { get; init; }

    public byte CdefBits { get; init; }

    public byte DeltaQRes { get; init; }

    public byte DeltaLfRes { get; init; }

    public NriVideoAV1TileLayoutDesc? TileLayout { get; init; }

    public VideoAV1QuantizationDesc? Quantization { get; init; }

    public VideoAV1LoopFilterDesc? LoopFilter { get; init; }

    public VideoAV1CdefDesc? Cdef { get; init; }

    public VideoAV1SegmentationDesc? Segmentation { get; init; }

    public VideoAV1LoopRestorationDesc? LoopRestoration { get; init; }

    public VideoAV1GlobalMotionDesc? GlobalMotion { get; init; }

    public VideoAV1FilmGrainDesc? FilmGrain { get; init; }

    /// <summary>Optional; all 8 entries when provided.</summary>
    public NriVideoAV1OrderHints? OrderHints { get; init; }

    /// <summary><c>skipModeFrames</c>: the two <c>SkipModeFrame</c> names, used with <c>SKIP_MODE_PRESENT</c>.</summary>
    public VideoAV1ReferenceName SkipModeFrame0 { get; init; }

    public VideoAV1ReferenceName SkipModeFrame1 { get; init; }

    public ReadOnlyMemory<VideoAV1DecodeTileDesc> Tiles { get; init; }

    public ReadOnlyMemory<NriVideoAV1ReferenceDesc> References { get; init; }
}

/// <summary><c>VideoAV1EncodeDecodeInfoDesc</c> with its required structs by value and its arrays as memory.</summary>
public readonly record struct NriVideoAV1EncodeDecodeInfoDesc
{
    /// <summary>The feedback of the encode, from <c>GetVideoEncodeFeedback</c>.</summary>
    public VideoEncodeFeedback Feedback { get; init; }

    /// <summary>The sequence the session encodes.</summary>
    public VideoAV1SequenceDesc Sequence { get; init; }

    public ReadOnlyMemory<byte> EncodedPayloadHeader { get; init; }

    /// <summary>The previous DPB snapshot; required for inter frames. Without it the frame is described as a key frame.</summary>
    public ReadOnlyMemory<NriVideoAV1ReferenceDesc> References { get; init; }

    /// <summary>The submitted picture description; required when <see cref="EncodedPayloadHeader"/> is empty (D3D12 answers <c>UNSUPPORTED</c> for a header).</summary>
    public VideoEncodePictureDesc? PictureDesc { get; init; }

    /// <summary>The submitted AV1 picture description; required when <see cref="EncodedPayloadHeader"/> is empty.</summary>
    public NriVideoAV1EncodePictureDesc? Av1PictureDesc { get; init; }

    /// <summary>Optional; 8 entries indexed by <c>refFrameIndex</c> when provided: the saved state of the frame that last refreshed each slot.</summary>
    public ReadOnlyMemory<VideoAV1SavedFrameState> SavedFrameStates { get; init; }
}
