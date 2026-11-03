namespace SharpNRI.Native;

public unsafe partial struct UpscalerDesc
{
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t upscaleResolution;

    [NativeTypeName("nri::UpscalerType")]
    public UpscalerType type;

    [NativeTypeName("nri::UpscalerMode")]
    public UpscalerMode mode;

    [NativeTypeName("nri::UpscalerBits")]
    public UpscalerBits flags;

    [NativeTypeName("uint8_t")]
    public byte preset;

    [NativeTypeName("nri::Format")]
    public Format outputFormat;

    [NativeTypeName("nri::CommandBuffer *")]
    public CommandBuffer* commandBuffer;
}
