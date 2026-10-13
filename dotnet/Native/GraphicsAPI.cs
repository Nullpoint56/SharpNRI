using System;
namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
[Flags]
public enum GraphicsAPI : byte
{
    NONE = (1 << 0),
    D3D11 = (1 << 1),
    D3D12 = (1 << 2),
    VK = (1 << 3),
    WGPU = (1 << 4),
    METAL = (1 << 5),
}
