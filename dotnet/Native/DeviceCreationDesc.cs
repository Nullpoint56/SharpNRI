namespace PrismNRI.Native;

public unsafe partial struct DeviceCreationDesc
{
    [NativeTypeName("nri::GraphicsAPI")]
    public GraphicsAPI graphicsAPI;

    [NativeTypeName("nri::Robustness")]
    public Robustness robustness;

    [NativeTypeName("nri::DeviceLostInfoLevel")]
    public DeviceLostInfoLevel deviceLostInfoLevel;

    [NativeTypeName("const AdapterDesc *")]
    public AdapterDesc* adapterDesc;

    [NativeTypeName("nri::CallbackInterface")]
    public CallbackInterface callbackInterface;

    [NativeTypeName("nri::AllocationCallbacks")]
    public AllocationCallbacks allocationCallbacks;

    [NativeTypeName("const QueueFamilyDesc *")]
    public QueueFamilyDesc* queueFamilies;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyNum;

    [NativeTypeName("uint32_t")]
    public uint d3dShaderExtRegister;

    [NativeTypeName("uint32_t")]
    public uint d3dZeroBufferSize;

    [NativeTypeName("const char *")]
    public sbyte* d3d12AgilitySdkPath;

    [NativeTypeName("nri::VKBindingOffsets")]
    public VKBindingOffsets vkBindingOffsets;

    [NativeTypeName("nri::VKExtensions")]
    public VKExtensions vkExtensions;

    [NativeTypeName("bool")]
    public byte enableNRIValidation;

    [NativeTypeName("bool")]
    public byte enableGraphicsAPIValidation;

    [NativeTypeName("bool")]
    public byte enableD3D11CommandBufferEmulation;

    [NativeTypeName("bool")]
    public byte enableD3D12RayTracingValidation;

    [NativeTypeName("bool")]
    public byte enableD3D12GpuBasedValidation;

    [NativeTypeName("bool")]
    public byte enableMemoryZeroInitialization;

    [NativeTypeName("bool")]
    public byte disableVKRayTracing;

    [NativeTypeName("bool")]
    public byte disableD3D12EnhancedBarriers;

    [NativeTypeName("bool")]
    public byte disableD3D12BreakOnError;
}
