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

    [NativeTypeName("nri::Dim_t")]
    public ushort mipNum;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerOffset;

    [NativeTypeName("nri::Dim_t")]
    public ushort layerNum;

    [NativeTypeName("nri::PlaneBits")]
    public PlaneBits planes;

    [NativeTypeName("nri::Queue *")]
    public Queue* srcQueue;

    [NativeTypeName("nri::Queue *")]
    public Queue* dstQueue;
}
