namespace SharpNRI.Native;

public unsafe partial struct UpscalerDesc
{
    /// <summary>output resolution</summary>
    [NativeTypeName("nri::Dim2_t")]
    public Dim2_t upscaleResolution;

    [NativeTypeName("nri::UpscalerType")]
    public UpscalerType type;

    /// <summary>selects the scaling factor reported by "GetUpscalerProps"</summary>
    [NativeTypeName("nri::UpscalerMode")]
    public UpscalerMode mode;

    [NativeTypeName("nri::UpscalerBits")]
    public UpscalerBits flags;

    /// <summary>0: default; DLSR/DLRR: NGX preset; XESS: SDK network model + 1</summary>
    [NativeTypeName("uint8_t")]
    public byte preset;

    /// <summary>needed only for NIS, if "shaderFeatures.storageWriteWithoutFormat" is unsupported</summary>
    [NativeTypeName("nri::Format")]
    public Format outputFormat;

    /// <summary>DLSR/DLRR: graphics or compute command buffer in recording state; caller submits it (internal submission and wait otherwise)</summary>
    [NativeTypeName("nri::CommandBuffer *")]
    public CommandBuffer* commandBuffer;
}
