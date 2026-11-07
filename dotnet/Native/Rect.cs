namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/refpages/latest/refpages/source/VkRect2D.html</summary>
public partial struct Rect
{
    [NativeTypeName("int16_t")]
    public short x;

    [NativeTypeName("int16_t")]
    public short y;

    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    [NativeTypeName("nri::Dim_t")]
    public ushort height;
}
