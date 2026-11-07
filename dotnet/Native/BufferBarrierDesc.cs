namespace SharpNRI.Native;

public unsafe partial struct BufferBarrierDesc
{
    /// <summary>use "GetAccelerationStructureBuffer" and "GetMicromapBuffer" for related barriers</summary>
    [NativeTypeName("nri::Buffer *")]
    public Buffer* buffer;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage before;

    [NativeTypeName("nri::AccessStage")]
    public AccessStage after;

    /// <summary>Only for ownership transfers of shared buffers to or from an external user: use "EXTERNAL_QUEUE" and the local queue executing the barrier</summary>
    [NativeTypeName("nri::Queue *")]
    public Queue* srcQueue;

    [NativeTypeName("nri::Queue *")]
    public Queue* dstQueue;
}
