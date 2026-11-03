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

    [NativeTypeName("uint32_t")]
    public uint d3dShaderExtRegister;

    [NativeTypeName("uint32_t")]
    public uint d3dZeroBufferSize;

    [NativeTypeName("bool")]
    public byte enableNRIValidation;

    [NativeTypeName("bool")]
    public byte enableMemoryZeroInitialization;

    [NativeTypeName("bool")]
    public byte disableD3D12EnhancedBarriers;

    [NativeTypeName("bool")]
    public byte disableNVAPIInitialization;
}
