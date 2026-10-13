namespace PrismNRI.Native;

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
