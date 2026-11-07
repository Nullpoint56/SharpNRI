namespace SharpNRI.Native;

/// <summary>The viewport origin is top-left (D3D native) by default, but can be changed to bottom-left (VK native) https://docs.vulkan.org/refpages/latest/refpages/source/VkViewport.html https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_viewport</summary>
public partial struct Viewport
{
    public float x;

    public float y;

    public float width;

    public float height;

    public float depthMin;

    public float depthMax;

    /// <summary>expects "features.viewportOriginBottomLeft"</summary>
    [NativeTypeName("bool")]
    public byte originBottomLeft;
}
