namespace SharpNRI.Native;

public unsafe partial struct SwapChainDesc
{
    [NativeTypeName("nri::Window")]
    public Window window;

    [NativeTypeName("const Queue *")]
    public Queue* queue;

    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    [NativeTypeName("nri::Dim_t")]
    public ushort height;

    [NativeTypeName("uint8_t")]
    public byte textureNum;

    [NativeTypeName("nri::SwapChainFormat")]
    public SwapChainFormat format;

    [NativeTypeName("nri::SwapChainBits")]
    public SwapChainBits flags;

    [NativeTypeName("uint8_t")]
    public byte queuedFrameNum;

    [NativeTypeName("nri::Scaling")]
    public Scaling scaling;

    [NativeTypeName("nri::Gravity")]
    public Gravity gravityX;

    [NativeTypeName("nri::Gravity")]
    public Gravity gravityY;
}
