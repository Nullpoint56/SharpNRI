namespace SharpNRI.Native;

public partial struct Viewport
{
    public float x;

    public float y;

    public float width;

    public float height;

    public float depthMin;

    public float depthMax;

    [NativeTypeName("bool")]
    public byte originBottomLeft;
}
