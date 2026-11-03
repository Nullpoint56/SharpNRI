namespace SharpNRI.Native;

public partial struct Color32i
{
    [NativeTypeName("int32_t")]
    public int x;

    [NativeTypeName("int32_t")]
    public int y;

    [NativeTypeName("int32_t")]
    public int z;

    [NativeTypeName("int32_t")]
    public int w;
}
