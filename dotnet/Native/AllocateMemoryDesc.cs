namespace PrismNRI.Native;

public partial struct AllocateMemoryDesc
{
    [NativeTypeName("uint64_t")]
    public ulong size;

    [NativeTypeName("nri::MemoryType")]
    public uint type;

    public float priority;

    [NativeTypeName("__AnonymousRecord_NRIDescs_L832_C5")]
    public _vma_e__Struct vma;

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
