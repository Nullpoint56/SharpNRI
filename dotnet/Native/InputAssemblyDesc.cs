namespace SharpNRI.Native;

public partial struct InputAssemblyDesc
{
    [NativeTypeName("nri::Topology")]
    public Topology topology;

    [NativeTypeName("uint8_t")]
    public byte tessControlPointNum;

    [NativeTypeName("nri::PrimitiveRestart")]
    public PrimitiveRestart primitiveRestart;
}
