namespace SharpNRI.Native;

public unsafe partial struct TextureSubresourceUploadDesc
{
    [NativeTypeName("const void *")]
    public void* slices;

    [NativeTypeName("uint32_t")]
    public uint sliceNum;

    [NativeTypeName("uint32_t")]
    public uint rowPitch;

    [NativeTypeName("uint32_t")]
    public uint slicePitch;
}
