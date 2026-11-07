namespace SharpNRI.Native;

/// <summary>Threadsafe: yes by default (see NRI_STREAMER_THREAD_SAFE CMake option) Different batches can be populated concurrently; using the same batch concurrently requires external synchronization</summary>
public unsafe partial struct StreamerInterface
{
    [NativeTypeName("Result (*)(Device &, const StreamerDesc &, Streamer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, StreamerDesc*, Streamer**, Result> CreateStreamer;

    [NativeTypeName("void (*)(Streamer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void> DestroyStreamer;

    /// <summary>Statically allocated (never changes)</summary>
    [NativeTypeName("Buffer *(*)(Streamer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, Buffer*> GetStreamerConstantBuffer;

    /// <summary>(HOST) Streams data to a constant buffer. Returns "offset" in "GetStreamerConstantBuffer" for direct usage in the current frame</summary>
    [NativeTypeName("uint32_t (*)(Streamer &, const void *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void*, uint, uint> StreamConstantData;

    /// <summary>(HOST) Streams generic CPU data. Returns a read/write pointer that remains valid until the ring buffer wraps. "placementAlignment" must be a power of 2 (0 is treated as 1)</summary>
    [NativeTypeName("void *(*)(Streamer &, const void *, uint64_t, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void*, ulong, uint, void*> StreamHostData;

    /// <summary>Copy batch identifier (unconsumed batches are discarded by "EndStreamerFrame")</summary>
    [NativeTypeName("StreamerCopyBatch (*)(Streamer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, ulong> BeginStreamerCopyBatch;

    /// <summary>(HOST) Stream data to a dynamic buffer. Return "buffer &amp; offset" for direct usage in the current frame</summary>
    [NativeTypeName("BufferOffset (*)(Streamer &, const StreamBufferDataDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, StreamBufferDataDesc*, BufferOffset> StreamBufferData;

    [NativeTypeName("BufferOffset (*)(Streamer &, const StreamTextureDataDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, StreamTextureDataDesc*, BufferOffset> StreamTextureData;

    /// <summary>Command buffer { (DEVICE) Copies batched data to destinations, which must be in "COPY_DESTINATION" state. Consumes "copyBatch", making it invalid</summary>
    [NativeTypeName("void (*)(CommandBuffer &, Streamer &, StreamerCopyBatch) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Streamer*, ulong, void> CmdCopyStreamedData;

    /// <summary>(HOST) Must be called once at the very end of the frame (also discards unconsumed copy batches)</summary>
    [NativeTypeName("void (*)(Streamer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void> EndStreamerFrame;
}
