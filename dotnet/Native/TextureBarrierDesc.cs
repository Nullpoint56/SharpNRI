namespace SharpNRI.Native;

public unsafe partial struct TextureBarrierDesc
{
    [NativeTypeName("nri::Texture *")]
    public Texture* texture;

    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage before;

    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage after;

    [NativeTypeName("nri::Dim_t")]
    public ushort mipOffset;

    /// <summary>can be "REMAINING"</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort mipNum;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerOffset;

    /// <summary>can be "REMAINING"</summary>
    [NativeTypeName("nri::Dim_t")]
    public ushort layerNum;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;

    /// <summary>Specify both queues for a cross-queue hand-off; only "SharingMode::EXCLUSIVE" may need local ownership transfers. Transfers to or from an external user apply to any sharing mode: use "EXTERNAL_QUEUE" and the local queue executing the barrier</summary>
    [NativeTypeName("nri::Queue *")]
    public Queue* srcQueue;

    [NativeTypeName("nri::Queue *")]
    public Queue* dstQueue;
}
