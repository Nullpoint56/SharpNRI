namespace PrismNRI.Native;

public unsafe partial struct BufferBarrierDesc
{
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage before;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage after;

    [NativeTypeName("nri::Queue *")]
    public Queue* srcQueue;

    [NativeTypeName("nri::Queue *")]
    public Queue* dstQueue;
}
