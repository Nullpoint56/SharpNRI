namespace SharpNRI.Native;

public unsafe partial struct VKExtensions
{
    [NativeTypeName("const char *const *")]
    public sbyte** instanceExtensions;

    [NativeTypeName("uint32_t")]
    public uint instanceExtensionNum;

    [NativeTypeName("const char *const *")]
    public sbyte** deviceExtensions;

    [NativeTypeName("uint32_t")]
    public uint deviceExtensionNum;
}
