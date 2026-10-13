namespace PrismNRI.Native;

public unsafe partial struct StreamerInterface
{
    [NativeTypeName("Result (*)(Device &, const StreamerDesc &, Streamer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, StreamerDesc*, Streamer**, Result> CreateStreamer;

    [NativeTypeName("void (*)(Streamer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void> DestroyStreamer;

    [NativeTypeName("Buffer *(*)(Streamer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, Buffer*> GetStreamerConstantBuffer;

    [NativeTypeName("uint32_t (*)(Streamer &, const void *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void*, uint, uint> StreamConstantData;

    [NativeTypeName("void *(*)(Streamer &, const void *, uint64_t, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void*, ulong, uint, void*> StreamHostData;

    [NativeTypeName("StreamerCopyBatch (*)(Streamer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, ulong> BeginStreamerCopyBatch;

    [NativeTypeName("BufferOffset (*)(Streamer &, const StreamBufferDataDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, StreamBufferDataDesc*, BufferOffset> StreamBufferData;

    [NativeTypeName("BufferOffset (*)(Streamer &, const StreamTextureDataDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, StreamTextureDataDesc*, BufferOffset> StreamTextureData;

    [NativeTypeName("void (*)(CommandBuffer &, Streamer &, StreamerCopyBatch) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Streamer*, ulong, void> CmdCopyStreamedData;

    [NativeTypeName("void (*)(Streamer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Streamer*, void> EndStreamerFrame;
}
