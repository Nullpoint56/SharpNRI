namespace PrismNRI.Native;

public unsafe partial struct SharedHandleInterface
{
    [NativeTypeName("Result (*)(Device &, MemoryLocation, float, const TextureDesc &, Texture *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryLocation, float, TextureDesc*, Texture**, Result> CreateSharedTexture;

    [NativeTypeName("Result (*)(Device &, uint64_t, Fence *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ulong, Fence**, Result> CreateSharedFence;

    [NativeTypeName("Result (*)(const Texture &, SharedHandle &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, SharedHandle*, Result> GetTextureSharedHandle;

    [NativeTypeName("Result (*)(const Fence &, SharedHandle &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Fence*, SharedHandle*, Result> GetFenceSharedHandle;
}
