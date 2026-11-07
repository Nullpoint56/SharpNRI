namespace SharpNRI.Native;

/// <summary>D3D12: process-global settings affect all subsequently created D3D12 devices; later NRI device creation does not disable them</summary>
public unsafe partial struct DeviceCreationDesc
{
    [NativeTypeName("nri::GraphicsAPI")]
    public GraphicsAPI graphicsAPI;

    [NativeTypeName("nri::Robustness")]
    public Robustness robustness;

    /// <summary>D3D12: process-global</summary>
    [NativeTypeName("nri::DeviceLostInfoLevel")]
    public DeviceLostInfoLevel deviceLostInfoLevel;

    [NativeTypeName("const AdapterDesc *")]
    public AdapterDesc* adapterDesc;

    [NativeTypeName("nri::CallbackInterface")]
    public CallbackInterface callbackInterface;

    [NativeTypeName("nri::AllocationCallbacks")]
    public AllocationCallbacks allocationCallbacks;

    /// <summary>One "GRAPHICS" queue is created by default</summary>
    [NativeTypeName("const QueueFamilyDesc *")]
    public QueueFamilyDesc* queueFamilies;

    /// <summary>put "GRAPHICS" queue at the beginning of the list</summary>
    [NativeTypeName("uint32_t")]
    public uint queueFamilyNum;

    /// <summary>D3D specific vendor specific shader extensions (default is "NRI_SHADER_EXT_REGISTER", space is always "0")</summary>
    [NativeTypeName("uint32_t")]
    public uint d3dShaderExtRegister;

    /// <summary>no "memset" functionality in D3D, "CmdZeroBuffer" implemented via a bunch of copies (4 Mb by default)</summary>
    [NativeTypeName("uint32_t")]
    public uint d3dZeroBufferSize;

    /// <summary>directory holding "D3D12Core.dll" of the Agility SDK NRI was built against; if set, the SDK is loaded through "ID3D12SDKConfiguration1::CreateDeviceFactory" and applied to the process, so the host executable needs no "D3D12SDKVersion" / "D3D12SDKPath" exports (requires "NRI_ENABLE_AGILITY_SDK_SUPPORT")</summary>
    [NativeTypeName("const char *")]
    public sbyte* d3d12AgilitySdkPath;

    /// <summary>Vulkan specific</summary>
    [NativeTypeName("nri::VKBindingOffsets")]
    public VKBindingOffsets vkBindingOffsets;

    /// <summary>to enable</summary>
    [NativeTypeName("nri::VKExtensions")]
    public VKExtensions vkExtensions;

    /// <summary>appended to "VkInstanceCreateInfo::pNext" after NRI's "VkDebugUtilsMessengerCreateInfoEXT" and "VkValidationFeaturesEXT" (i.e. "VkLayerSettingsCreateInfoEXT")</summary>
    [NativeTypeName("const void *")]
    public void* vkInstanceCreateInfoNext;

    /// <summary>Switches (disabled by default) embedded validation layer, checks for NRI specifics</summary>
    [NativeTypeName("bool")]
    public byte enableNRIValidation;

    /// <summary>GAPI-provided validation layer (D3D12: process-global; METAL: ignored, set "MTL_DEBUG_LAYER=1" instead)</summary>
    [NativeTypeName("bool")]
    public byte enableGraphicsAPIValidation;

    /// <summary>enable? but why? (auto-enabled if deferred contexts are not supported)</summary>
    [NativeTypeName("bool")]
    public byte enableD3D11CommandBufferEmulation;

    /// <summary>slow but useful, can only be enabled if envvar "NV_ALLOW_RAYTRACING_VALIDATION" is set to "1"</summary>
    [NativeTypeName("bool")]
    public byte enableD3D12RayTracingValidation;

    /// <summary>D3D12: "ID3D12Debug1::SetEnableGPUBasedValidation" (slow, process-global, requires "enableGraphicsAPIValidation")</summary>
    [NativeTypeName("bool")]
    public byte enableD3D12GpuBasedValidation;

    /// <summary>page-clears are fast, but memory is not cleared by default in VK</summary>
    [NativeTypeName("bool")]
    public byte enableMemoryZeroInitialization;

    /// <summary>Switches (enabled by default) to save CPU memory in some implementations</summary>
    [NativeTypeName("bool")]
    public byte disableVKRayTracing;

    /// <summary>even if AgilitySDK is in use, some apps still use legacy barriers. It can be important for integrations</summary>
    [NativeTypeName("bool")]
    public byte disableD3D12EnhancedBarriers;

    /// <summary>D3D12: don't "SetBreakOnSeverity" for "ERROR" and "CORRUPTION" (messages still go through "CallbackInterface", if supported)</summary>
    [NativeTypeName("bool")]
    public byte disableD3D12BreakOnError;
}
