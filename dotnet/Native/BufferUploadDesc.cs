namespace SharpNRI.Native;

public unsafe partial struct BufferUploadDesc
{
    /// <summary>if provided, must be data for the whole buffer</summary>
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage after;
}
