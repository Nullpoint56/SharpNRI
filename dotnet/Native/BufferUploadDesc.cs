namespace PrismNRI.Native;

public unsafe partial struct BufferUploadDesc
{
    [NativeTypeName("const void *")]
    public void* data;

    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage after;
}
