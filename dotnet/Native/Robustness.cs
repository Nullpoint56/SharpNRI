namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum Robustness : byte
{
    DEFAULT,
    OFF,
    VK,
    D3D12,
    MAX_NUM,
}
