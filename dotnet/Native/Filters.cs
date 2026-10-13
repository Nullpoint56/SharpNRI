namespace PrismNRI.Native;

public partial struct Filters
{
    [NativeTypeName("nri::Filter")]
    public Filter min;

    [NativeTypeName("nri::Filter")]
    public Filter mag;

    [NativeTypeName("nri::Filter")]
    public Filter mip;

    [NativeTypeName("nri::FilterOp")]
    public FilterOp op;
}
