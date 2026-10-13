namespace PrismNRI.Native;

public partial struct DispatchRaysDesc
{
    [NativeTypeName("nri::StridedBufferRegion")]
    public StridedBufferRegion raygenShaderRecord;

    [NativeTypeName("nri::StridedBufferRegion")]
    public StridedBufferRegion missShaderBindingTable;

    [NativeTypeName("nri::StridedBufferRegion")]
    public StridedBufferRegion hitShaderBindingTable;

    [NativeTypeName("nri::StridedBufferRegion")]
    public StridedBufferRegion callableShaderBindingTable;

    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    [NativeTypeName("uint32_t")]
    public uint depth;
}
