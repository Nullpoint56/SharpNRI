namespace SharpNRI.Native;

public unsafe partial struct BufferViewDesc
{
    [NativeTypeName("const Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::BufferView")]
    public BufferView type;

    /// <summary>expects "memoryAlignment.bufferShaderResourceOffset" for shader resources</summary>
    [NativeTypeName("uint64_t")]
    public ulong offset;

    /// <summary>can be "WHOLE_SIZE"</summary>
    [NativeTypeName("uint64_t")]
    public ulong size;

    /// <summary>needed for typed views, i.e. "BUFFER" and "STORAGE_BUFFER"</summary>
    [NativeTypeName("nri::Format")]
    public Format format;

    /// <summary>needed for structured views, i.e. "STRUCTURED_BUFFER" and "STORAGE_STRUCTURED_BUFFER" (= "BufferDesc::structureStride", if not provided)</summary>
    [NativeTypeName("uint32_t")]
    public uint structureStride;
}
