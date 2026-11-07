namespace SharpNRI.Native;

/// <summary>Threadsafe: yes</summary>
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

    /// <summary>Command buffer { Dispatch (changes descriptor pool, pipeline layout and pipeline, barriers are externally controlled) MetalFX: - Native creation occurs on the first dispatch and may cause a hitch - Resource dimensions and formats must remain unchanged after the first dispatch</summary>
    [NativeTypeName("void (*)(CommandBuffer &, Upscaler &, const DispatchUpscaleDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Upscaler*, DispatchUpscaleDesc*, void> CmdDispatchUpscale;
}
