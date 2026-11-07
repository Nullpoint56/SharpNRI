namespace SharpNRI.Native;

public unsafe partial struct VertexBufferDesc
{
    [NativeTypeName("const Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("uint64_t")]
    public ulong offset;

    /// <summary>requires "features.extendedDynamicState", ignored otherwise, use "VertexStreamDesc::stride" instead</summary>
    [NativeTypeName("uint32_t")]
    public uint stride;
}
