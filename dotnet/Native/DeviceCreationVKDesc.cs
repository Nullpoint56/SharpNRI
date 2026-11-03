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

    [NativeTypeName("uint8_t")]
    public byte minorVersion;

    [NativeTypeName("bool")]
    public byte enableNRIValidation;

    [NativeTypeName("bool")]
    public byte enableMemoryZeroInitialization;
}
