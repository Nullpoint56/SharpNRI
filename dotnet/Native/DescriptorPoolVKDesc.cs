namespace SharpNRI.Native;

public partial struct DescriptorPoolVKDesc
{
    [NativeTypeName("VKNonDispatchableHandle")]
    public ulong vkDescriptorPool;

    [NativeTypeName("uint32_t")]
    public uint descriptorSetMaxNum;
}
