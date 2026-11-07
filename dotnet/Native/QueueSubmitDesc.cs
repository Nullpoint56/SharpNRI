namespace SharpNRI.Native;

public unsafe partial struct QueueSubmitDesc
{
    [NativeTypeName("const FenceSubmitDesc *")]
    public FenceSubmitDesc* waitFences;

    [NativeTypeName("uint32_t")]
    public uint waitFenceNum;

    [NativeTypeName("const CommandBuffer *const *")]
    public CommandBuffer** commandBuffers;

    [NativeTypeName("uint32_t")]
    public uint commandBufferNum;

    [NativeTypeName("const FenceSubmitDesc *")]
    public FenceSubmitDesc* signalFences;

    [NativeTypeName("uint32_t")]
    public uint signalFenceNum;

    /// <summary>Required if "NRILowLatency" is enabled for the swap chain</summary>
    [NativeTypeName("const SwapChain *")]
    public SwapChain* swapChain;

    /// <summary>must match the value passed to "QueuePresent" for the frame</summary>
    [NativeTypeName("uint64_t")]
    public ulong presentId;
}
