namespace SharpNRI.Native;

public unsafe partial struct VideoDecodeArgument
{
    [NativeTypeName("nri::VideoDecodeArgumentType")]
    public VideoDecodeArgumentType type;

    [NativeTypeName("uint32_t")]
    public uint size;

    [NativeTypeName("const void *")]
    public void* data;
}
