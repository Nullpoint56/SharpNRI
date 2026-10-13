namespace PrismNRI.Native;

public unsafe partial struct UpscalerInterface
{
    [NativeTypeName("Result (*)(Device &, const UpscalerDesc &, Upscaler *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, UpscalerDesc*, Upscaler**, Result> CreateUpscaler;

    [NativeTypeName("void (*)(Upscaler *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Upscaler*, void> DestroyUpscaler;

    [NativeTypeName("bool (*)(const Device &, UpscalerType) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, UpscalerType, byte> IsUpscalerSupported;

    [NativeTypeName("void (*)(const Upscaler &, UpscalerProps &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Upscaler*, UpscalerProps*, void> GetUpscalerProps;

    [NativeTypeName("void (*)(CommandBuffer &, Upscaler &, const DispatchUpscaleDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Upscaler*, DispatchUpscaleDesc*, void> CmdDispatchUpscale;
}
