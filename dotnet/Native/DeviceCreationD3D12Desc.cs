namespace SharpNRI.Native;

public unsafe partial struct DeviceCreationD3D12Desc
{
    public ID3D12Device* d3d12Device;

    [NativeTypeName("const QueueFamilyD3D12Desc *")]
    public QueueFamilyD3D12Desc* queueFamilies;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyNum;

    public AGSContext* agsContext;

    [NativeTypeName("nri::CallbackInterface")]
    public CallbackInterface callbackInterface;

    [NativeTypeName("nri::AllocationCallbacks")]
    public AllocationCallbacks allocationCallbacks;

    /// <summary>vendor specific shader extensions (default is "NRI_SHADER_EXT_REGISTER", space is always "0")</summary>
    [NativeTypeName("uint32_t")]
    public uint d3dShaderExtRegister;

    /// <summary>no "memset" functionality in D3D, "CmdZeroBuffer" implemented via a bunch of copies (4 Mb by default)</summary>
    [NativeTypeName("uint32_t")]
    public uint d3dZeroBufferSize;

    /// <summary>Switches (disabled by default)</summary>
    [NativeTypeName("bool")]
    public byte enableNRIValidation;

    /// <summary>page-clears are fast, not enabled by default to match VK (the extension needed)</summary>
    [NativeTypeName("bool")]
    public byte enableMemoryZeroInitialization;

    /// <summary>Switches (enabled by default) even if AgilitySDK is in use, some apps still use legacy barriers. It can be important for integrations</summary>
    [NativeTypeName("bool")]
    public byte disableD3D12EnhancedBarriers;

    /// <summary>at least NVAPI requires calling "NvAPI_Initialize" in DLL/EXE where the device is created</summary>
    [NativeTypeName("bool")]
    public byte disableNVAPIInitialization;
}
