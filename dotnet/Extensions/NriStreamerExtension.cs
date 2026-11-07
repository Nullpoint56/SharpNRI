// L3 — the Streamer extension (NRIStreamer.h).
//
// A per-frame upload helper built on core NRI: a ring of dynamic constants, a growable dynamic
// buffer that data is streamed into and optionally copied from to a destination buffer or texture,
// and a host-memory ring. The streamer is an object the extension creates and destroys
// (CreateStreamer / DestroyStreamer), so it is an NriObject counted against the device like any
// other.
//
// NRI's usage contract, from the header:
//  - stream constants, host data or dynamic-buffer data as needed;
//  - for destination copies, BeginStreamerCopyBatch, name the batch in each StreamBufferData /
//    StreamTextureData, and record CmdCopyStreamedData once (it consumes the batch);
//  - call EndStreamerFrame once after all command recording that uses streamed data is complete
//    (it also discards unconsumed batches).
// Thread safe by default (NRI_STREAMER_THREAD_SAFE); different batches may be populated
// concurrently, the same batch needs external synchronization.
//
// Buffers NRI hands back (the constant buffer, the dynamic buffer in a BufferOffset) belong to the
// streamer: they come back as non-owning NriBuffer wrappers, cached so the per-frame calls do not
// allocate, and invalidated when the streamer is disposed.

using System.Buffers;
using SharpNRI.Interop;
using SharpNRI.Native;
using Buffer = SharpNRI.Native.Buffer;

namespace SharpNRI;

/// <summary><c>BufferOffset</c>: a buffer and an offset into it.</summary>
/// <param name="Buffer">Null when NRI could not place the data (it reports why through the message callback).</param>
/// <param name="Offset">Byte offset of the data in <paramref name="Buffer"/>.</param>
public readonly record struct NriBufferOffset(NriBuffer? Buffer, ulong Offset);

/// <summary>
/// <c>StreamBufferDataDesc</c> with its data chunks as memory and its destination as a wrapper.
/// The chunks are concatenated in dynamic buffer memory; NRI copies them during the call.
/// </summary>
public readonly ref struct NriStreamBufferDataDesc
{
    /// <summary>Data to upload, concatenated in order.</summary>
    public ReadOnlySpan<ReadOnlyMemory<byte>> DataChunks { get; init; }

    /// <summary>Desired alignment of the returned offset.</summary>
    public uint PlacementAlignment { get; init; }

    /// <summary>Required when <see cref="DstBuffer"/> is set: from <see cref="NriStreamerExtension.BeginStreamerCopyBatch"/>.</summary>
    public ulong CopyBatch { get; init; }

    /// <summary>Optional destination, copied to by <see cref="NriStreamerExtension.CopyStreamedData"/>.</summary>
    public NriBuffer? DstBuffer { get; init; }

    public ulong DstOffset { get; init; }
}

/// <summary><c>StreamTextureDataDesc</c> with its data as bytes and its destination as a wrapper.</summary>
public readonly ref struct NriStreamTextureDataDesc
{
    /// <summary>The texels, laid out with <see cref="DataRowPitch"/> and <see cref="DataSlicePitch"/>.</summary>
    public ReadOnlySpan<byte> Data { get; init; }

    /// <summary>Bytes from one row to the next in <see cref="Data"/>; for a block-compressed format, one row of blocks to the next.</summary>
    public uint DataRowPitch { get; init; }

    /// <summary>Bytes from one depth slice to the next in <see cref="Data"/>.</summary>
    public uint DataSlicePitch { get; init; }

    /// <summary>From <see cref="NriStreamerExtension.BeginStreamerCopyBatch"/>.</summary>
    public ulong CopyBatch { get; init; }

    public required NriTexture DstTexture { get; init; }

    /// <summary>Optional; the whole texture when default.</summary>
    public TextureRegionDesc DstRegion { get; init; }
}

/// <summary>A streamer. Created by <see cref="NriStreamerExtension.CreateStreamer"/>.</summary>
public sealed unsafe class NriStreamer : NriObject
{
    private readonly NriStreamerExtension _extension;
    private NriBuffer? _constantBuffer;
    private NriBuffer? _dynamicBuffer;

    internal NriStreamer(NriStreamerExtension extension, Streamer* handle, in StreamerDesc desc) : base(handle, "NriStreamer")
    {
        _extension = extension;
        Desc = desc;
        extension.Device.ChildCreated();
    }

    public override NriDevice Device => _extension.Device;

    /// <summary>The desc the streamer was created with.</summary>
    public StreamerDesc Desc { get; }

    internal Streamer* Raw => (Streamer*)Handle;

    /// <summary>A non-owning wrapper for a buffer the streamer owns; the last one is reused.</summary>
    internal NriBuffer? WrapDynamic(Buffer* buffer)
    {
        if (buffer is null) return null;
        var cached = Volatile.Read(ref _dynamicBuffer);
        if (cached is not null && !cached.IsDisposed && cached.Raw == buffer) return cached;
        var wrapped = new NriBuffer(Device, buffer, owned: false);
        Volatile.Write(ref _dynamicBuffer, wrapped);
        return wrapped;
    }

    internal NriBuffer? WrapConstant(Buffer* buffer)
    {
        if (buffer is null) return null;
        var cached = Volatile.Read(ref _constantBuffer);
        if (cached is not null && !cached.IsDisposed && cached.Raw == buffer) return cached;
        var wrapped = new NriBuffer(Device, buffer, owned: false);
        Volatile.Write(ref _constantBuffer, wrapped);
        return wrapped;
    }

    private protected override void Destroy(void* handle) => _extension.DestroyStreamer((Streamer*)handle);

    private protected override void OnDestroyed()
    {
        // The streamer's buffers are gone with it; make their wrappers say so.
        _constantBuffer?.Dispose();
        _dynamicBuffer?.Dispose();
        base.OnDestroyed();
    }
}

/// <summary>NRI's <c>StreamerInterface</c>. Acquire through <see cref="NriDevice.Streamer"/>.</summary>
public sealed unsafe class NriStreamerExtension
{
    private readonly StreamerInterface _table;

    internal NriStreamerExtension(NriDevice device, in StreamerInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriStreamerExtension? extension)
    {
        if (InterfaceTable.TryAcquire<StreamerInterface>(device.Raw, "StreamerInterface", out var table))
        {
            extension = new NriStreamerExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// A streamer. <c>dynamicBufferDesc.size</c> is the initial per-frame size of the dynamic
    /// buffer, allocated at creation (patch 0012; upstream ignores it and grows from empty): the
    /// buffer is <c>size</c> rounded up to 64 KiB times <c>queuedFrameNum</c>, and a frame that
    /// streams more than the per-frame size still grows it (a new committed buffer, the old one
    /// destroyed <c>queuedFrameNum</c> frames later by <see cref="EndStreamerFrame"/>). 0 grows on
    /// demand from empty, one committed buffer per new per-frame high-water mark.
    /// <c>queuedFrameNum</c> is the number of per-frame regions in the dynamic buffer, the frame
    /// being filled included: data streamed in a frame is overwritten <c>queuedFrameNum</c> frames
    /// later, so the GPU must be done with frame N before frame N + <c>queuedFrameNum</c> streams
    /// anything. NRI adds nothing (upstream's header says it adds one; the code stopped in 6f80a7c).
    /// The constant ring and the host-data ring are optional (size 0), but NRI validation requires
    /// <c>constantBufferMemoryLocation</c> to be an UPLOAD location even with no constant ring,
    /// although the header marks it optional (measured at v190).
    /// </summary>
    public NriStreamer CreateStreamer(in StreamerDesc streamerDesc)
    {
        Streamer* handle = null;
        fixed (StreamerDesc* p = &streamerDesc) NriException.Check(_table.CreateStreamer(Device.Raw, p, &handle), "CreateStreamer");
        return new NriStreamer(this, handle, streamerDesc);
    }

    internal void DestroyStreamer(Streamer* streamer) => _table.DestroyStreamer(streamer);

    /// <summary>The statically allocated constant ring buffer; owned by the streamer. Null without a constant ring.</summary>
    public NriBuffer? GetStreamerConstantBuffer(NriStreamer streamer) =>
        streamer.WrapConstant(_table.GetStreamerConstantBuffer(streamer.Raw));

    /// <summary>
    /// (HOST) Streams <paramref name="data"/> into the constant ring. Returns its offset in
    /// <see cref="GetStreamerConstantBuffer"/>, for direct use in the current frame.
    /// </summary>
    public uint StreamConstantData(NriStreamer streamer, ReadOnlySpan<byte> data)
    {
        fixed (byte* p = data) return _table.StreamConstantData(streamer.Raw, p, (uint)data.Length);
    }

    /// <summary><see cref="StreamConstantData(NriStreamer, ReadOnlySpan{byte})"/> for one value.</summary>
    public uint StreamConstantData<T>(NriStreamer streamer, in T value) where T : unmanaged
    {
        fixed (T* p = &value) return _table.StreamConstantData(streamer.Raw, p, (uint)sizeof(T));
    }

    /// <summary>
    /// (HOST) Copies <paramref name="data"/> into the host-data ring and returns the copy,
    /// read/write, valid until the ring wraps. <paramref name="placementAlignment"/> must be a
    /// power of 2 (0 is treated as 1). Empty when the ring has no room (NRI reports why).
    /// </summary>
    public Span<byte> StreamHostData(NriStreamer streamer, ReadOnlySpan<byte> data, uint placementAlignment = 0)
    {
        void* result;
        fixed (byte* p = data) result = _table.StreamHostData(streamer.Raw, p, (ulong)data.Length, placementAlignment);
        return result is null ? default : new Span<byte>(result, data.Length);
    }

    /// <summary>
    /// A copy batch identifier (never 0). <see cref="EndStreamerFrame"/> discards every batch not
    /// yet recorded by <see cref="CopyStreamedData"/>, so a batch is filled and recorded within one
    /// streamer frame; one filled on another thread reaches the recording thread before that
    /// frame's <see cref="EndStreamerFrame"/>.
    /// </summary>
    public ulong BeginStreamerCopyBatch(NriStreamer streamer) => _table.BeginStreamerCopyBatch(streamer.Raw);

    /// <summary>
    /// (HOST) Streams data into the dynamic buffer. Returns the buffer and offset for direct use
    /// in the current frame. Each chunk is pinned for the call only; NRI copies it.
    /// </summary>
    public NriBufferOffset StreamBufferData(NriStreamer streamer, in NriStreamBufferDataDesc streamBufferDataDesc)
    {
        var chunks = streamBufferDataDesc.DataChunks;
        var native = stackalloc DataSize[chunks.Length];
        var pins = chunks.Length == 0 ? [] : ArrayPool<MemoryHandle>.Shared.Rent(chunks.Length);
        try
        {
            for (var i = 0; i < chunks.Length; i++)
            {
                pins[i] = chunks[i].Pin();
                native[i] = new DataSize { data = pins[i].Pointer, size = (ulong)chunks[i].Length };
            }

            var desc = new StreamBufferDataDesc
            {
                dataChunks = native,
                dataChunkNum = (uint)chunks.Length,
                placementAlignment = streamBufferDataDesc.PlacementAlignment,
                copyBatch = streamBufferDataDesc.CopyBatch,
                dstBuffer = streamBufferDataDesc.DstBuffer is { } dst ? dst.Raw : null,
                dstOffset = streamBufferDataDesc.DstOffset,
            };
            var result = _table.StreamBufferData(streamer.Raw, &desc);
            return new NriBufferOffset(streamer.WrapDynamic(result.buffer), result.offset);
        }
        finally
        {
            for (var i = 0; i < chunks.Length; i++) pins[i].Dispose();
            if (pins.Length != 0) ArrayPool<MemoryHandle>.Shared.Return(pins, clearArray: true);
        }
    }

    /// <summary>
    /// <see cref="StreamBufferData(NriStreamer, in NriStreamBufferDataDesc)"/> for one chunk.
    /// <paramref name="copyBatch"/> is required when <paramref name="dstBuffer"/> is set.
    /// </summary>
    public NriBufferOffset StreamBufferData(NriStreamer streamer, ReadOnlySpan<byte> data, uint placementAlignment = 0, ulong copyBatch = 0, NriBuffer? dstBuffer = null, ulong dstOffset = 0)
    {
        fixed (byte* p = data)
        {
            var chunk = new DataSize { data = p, size = (ulong)data.Length };
            var desc = new StreamBufferDataDesc
            {
                dataChunks = &chunk,
                dataChunkNum = 1,
                placementAlignment = placementAlignment,
                copyBatch = copyBatch,
                dstBuffer = dstBuffer is null ? null : dstBuffer.Raw,
                dstOffset = dstOffset,
            };
            var result = _table.StreamBufferData(streamer.Raw, &desc);
            return new NriBufferOffset(streamer.WrapDynamic(result.buffer), result.offset);
        }
    }

    /// <summary>
    /// (HOST) Streams texels into the dynamic buffer for a copy to <c>DstTexture</c> by
    /// <see cref="CopyStreamedData"/>. Returns where the texels landed. A block-compressed format
    /// is staged in rows of blocks (NRI bc3ebe7; v190 staged texel rows, 16 times the space for
    /// 4x4 blocks, and read past the end of <c>Data</c>).
    /// </summary>
    public NriBufferOffset StreamTextureData(NriStreamer streamer, in NriStreamTextureDataDesc streamTextureDataDesc)
    {
        ArgumentNullException.ThrowIfNull(streamTextureDataDesc.DstTexture);
        fixed (byte* p = streamTextureDataDesc.Data)
        {
            var desc = new StreamTextureDataDesc
            {
                data = p,
                dataRowPitch = streamTextureDataDesc.DataRowPitch,
                dataSlicePitch = streamTextureDataDesc.DataSlicePitch,
                copyBatch = streamTextureDataDesc.CopyBatch,
                dstTexture = streamTextureDataDesc.DstTexture.Raw,
                dstRegion = streamTextureDataDesc.DstRegion,
            };
            var result = _table.StreamTextureData(streamer.Raw, &desc);
            return new NriBufferOffset(streamer.WrapDynamic(result.buffer), result.offset);
        }
    }

    /// <summary>
    /// <c>CmdCopyStreamedData</c> (DEVICE): copies the batch to its destinations, which must be in
    /// the <c>COPY_DESTINATION</c> state. Consumes <paramref name="copyBatch"/>.
    /// </summary>
    public void CopyStreamedData(NriCommandBuffer commandBuffer, NriStreamer streamer, ulong copyBatch) =>
        _table.CmdCopyStreamedData(commandBuffer.Raw, streamer.Raw, copyBatch);

    /// <summary>(HOST) Call once at the very end of the frame. Also discards unconsumed copy batches.</summary>
    public void EndStreamerFrame(NriStreamer streamer) => _table.EndStreamerFrame(streamer.Raw);
}
