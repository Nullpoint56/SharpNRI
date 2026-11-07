namespace SharpNRI.Native;

/// <summary>SwapChain textures will be created as "color attachment" resources queuedFrameNum = 0 - auto-selection between 1 (for waitable) or 2 (otherwise) queuedFrameNum = 2 - recommended if the GPU frame time is less than the desired frame time, but the sum of 2 frames is greater</summary>
public unsafe partial struct SwapChainDesc
{
    [NativeTypeName("nri::Window")]
    public Window window;

    /// <summary>GRAPHICS or COMPUTE (requires "features.presentFromCompute")</summary>
    [NativeTypeName("const Queue *")]
    public Queue* queue;

    [NativeTypeName("nri::Dim_t")]
    public ushort width;

    [NativeTypeName("nri::Dim_t")]
    public ushort height;

    /// <summary>desired value, real value must be queried using "GetSwapChainTextures"</summary>
    [NativeTypeName("uint8_t")]
    public byte textureNum;

    /// <summary>desired format, real value must be queried using "GetTextureDesc" for one of the swap chain textures</summary>
    [NativeTypeName("nri::SwapChainFormat")]
    public SwapChainFormat format;

    [NativeTypeName("nri::SwapChainBits")]
    public SwapChainBits flags;

    /// <summary>aka "max frame latency", aka "number of frames in flight" (mostly for D3D11)</summary>
    [NativeTypeName("uint8_t")]
    public byte queuedFrameNum;

    /// <summary>Present scaling and positioning, silently ignored if "features.resizableSwapChain" is not supported or not supported by the implicitly choosen present mode VK: if scaling is not supported, "OUT_OF_DATE" error is triggered on resizing</summary>
    [NativeTypeName("nri::Scaling")]
    public Scaling scaling;

    [NativeTypeName("nri::Gravity")]
    public Gravity gravityX;

    [NativeTypeName("nri::Gravity")]
    public Gravity gravityY;
}
