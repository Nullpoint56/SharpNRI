namespace SharpNRI.Native;

/// <summary>Using "CmdBarrier" inside a rendering pass is allowed, but only for "Layout::INPUT_ATTACHMENT" access transitions D3D12 filters out "transitioning to the same state" barriers if "features.enhancedBarriers" is not supported</summary>
public unsafe partial struct BarrierDesc
{
    [NativeTypeName("const GlobalBarrierDesc *")]
    public GlobalBarrierDesc* globals;

    [NativeTypeName("uint32_t")]
    public uint globalNum;

    [NativeTypeName("const BufferBarrierDesc *")]
    public BufferBarrierDesc* buffers;

    [NativeTypeName("uint32_t")]
    public uint bufferNum;

    [NativeTypeName("const TextureBarrierDesc *")]
    public TextureBarrierDesc* textures;

    [NativeTypeName("uint32_t")]
    public uint textureNum;
}
