namespace SharpNRI.Native;

/// <summary>A group of non-dedicated "MemoryDesc"s of the SAME "MemoryType" can be merged into a single memory allocation</summary>
public partial struct AllocateMemoryDesc
{
    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("nri::MemoryType")]
    public uint type;

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/direct3d12/residency https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_residency_priority https://docs.vulkan.org/refpages/latest/refpages/source/VkMemoryPriorityAllocateInfoEXT.html [-1; 1]: low &lt; 0, normal = 0, high &gt; 0</summary>
    public float priority;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L841_C5")]
    public _vma_e__Struct vma;

    /// <summary>If "false", may reduce alignment requirements</summary>
    [NativeTypeName("bool")]
    public byte allowMultisampleTextures;

    public partial struct _vma_e__Struct
    {
        [NativeTypeName("bool")]
        public byte enable;

        [NativeTypeName("uint32_t")]
        public uint alignment;
    }
}
