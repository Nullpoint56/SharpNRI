namespace SharpNRI.Native;

public partial struct DispatchRaysIndirectDesc
{
    [NativeTypeName("uint64_t")]
    public ulong raygenShaderRecordAddress;

    [NativeTypeName("uint64_t")]
    public ulong raygenShaderRecordSize;

    [NativeTypeName("uint64_t")]
    public ulong missShaderBindingTableAddress;

    [NativeTypeName("uint64_t")]
    public ulong missShaderBindingTableSize;

    [NativeTypeName("uint64_t")]
    public ulong missShaderBindingTableStride;

    [NativeTypeName("uint64_t")]
    public ulong hitShaderBindingTableAddress;

    [NativeTypeName("uint64_t")]
    public ulong hitShaderBindingTableSize;

    [NativeTypeName("uint64_t")]
    public ulong hitShaderBindingTableStride;

    [NativeTypeName("uint64_t")]
    public ulong callableShaderBindingTableAddress;

    [NativeTypeName("uint64_t")]
    public ulong callableShaderBindingTableSize;

    [NativeTypeName("uint64_t")]
    public ulong callableShaderBindingTableStride;

    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    [NativeTypeName("uint32_t")]
    public uint depth;
}
