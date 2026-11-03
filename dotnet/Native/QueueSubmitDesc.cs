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

    [NativeTypeName("const SwapChain *")]
    public SwapChain* swapChain;

    [NativeTypeName("uint64_t")]
    public ulong presentId;
}
