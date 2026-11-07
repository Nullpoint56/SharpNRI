namespace SharpNRI.Native;

public unsafe partial struct DeviceCreationVKDesc
{
    [NativeTypeName("nri::CallbackInterface")]
    public CallbackInterface callbackInterface;

    [NativeTypeName("nri::AllocationCallbacks")]
    public AllocationCallbacks allocationCallbacks;

    [NativeTypeName("const char *")]
    public sbyte* libraryPath;

    [NativeTypeName("nri::VKBindingOffsets")]
    public VKBindingOffsets vkBindingOffsets;

    /// <summary>enabled</summary>
    [NativeTypeName("nri::VKExtensions")]
    public VKExtensions vkExtensions;

    [NativeTypeName("VKHandle")]
    public void* vkInstance;

    [NativeTypeName("VKHandle")]
    public void* vkDevice;

    [NativeTypeName("VKHandle")]
    public void* vkPhysicalDevice;

    [NativeTypeName("const QueueFamilyVKDesc *")]
    public QueueFamilyVKDesc* queueFamilies;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyNum;

    /// <summary>&gt;= 2</summary>
    [NativeTypeName("uint8_t")]
    public byte minorVersion;

    /// <summary>Switches (disabled by default)</summary>
    [NativeTypeName("bool")]
    public byte enableNRIValidation;

    /// <summary>page-clears are fast, but memory is not cleared by default in VK</summary>
    [NativeTypeName("bool")]
    public byte enableMemoryZeroInitialization;
}
