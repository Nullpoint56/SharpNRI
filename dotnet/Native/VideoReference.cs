namespace SharpNRI.Native;

public unsafe partial struct VideoReference
{
    [NativeTypeName("nri::VideoPicture *")]
    public VideoPicture* picture;

    [NativeTypeName("uint32_t")]
    public uint slot;
}
