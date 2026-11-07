namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum DeviceLostInfoLevel : byte
{
    NONE,
    /// <summary>backend-supported basic device lost reporting</summary>
    BASIC,
    /// <summary>D3D12: additionally enables DRED breadcrumb contexts</summary>
    VERBOSE,
    MAX_NUM,
}
