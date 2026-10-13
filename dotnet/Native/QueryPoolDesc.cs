namespace PrismNRI.Native;

public partial struct QueryPoolDesc
{
    [NativeTypeName("nri::QueryType")]
    public QueryType queryType;

    [NativeTypeName("uint32_t")]
    public uint capacity;
}
