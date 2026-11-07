namespace SharpNRI.Native;

/// <summary>"AdapterDesc::supportedGraphicsAPIs" is a mask of supported graphics APIs</summary>
[NativeTypeName("uint8_t")]
[Flags]
public enum GraphicsAPI : byte
{
    /// <summary>lazy default for "desiredGraphicsAPIs" argument in "nriEnumerateAdapters"</summary>
    ALL = 0,
    /// <summary>Supports everything, does nothing, returns dummy non-NULL objects and ~0-filled descs, available if "NRI_ENABLE_NONE_SUPPORT = ON" in CMake</summary>
    NONE = (1 << 0),
    /// <summary>Direct3D 11 (feature set 11.1), available if "NRI_ENABLE_D3D11_SUPPORT = ON" in CMake (https://microsoft.github.io/DirectX-Specs/d3d/archive/D3D11_3_FunctionalSpec.htm)</summary>
    D3D11 = (1 << 1),
    /// <summary>Direct3D 12 (D3D12_SDK_VERSION 4 or 619+), available if "NRI_ENABLE_D3D12_SUPPORT = ON" in CMake (https://microsoft.github.io/DirectX-Specs/)</summary>
    D3D12 = (1 << 2),
    /// <summary>Vulkan 1.4+, 1.3++ or 1.2+++ (can be used on MacOS via MoltenVK), available if "NRI_ENABLE_VK_SUPPORT = ON" in CMake (https://registry.khronos.org/vulkan/specs/latest/html/vkspec.html)</summary>
    VK = (1 << 3),
    /// <summary>WebGPU via "wgpu-native", available if "NRI_ENABLE_WGPU_SUPPORT = ON" in CMake (https://github.com/gfx-rs/wgpu-native). Has limitations similar to D3D11</summary>
    WGPU = (1 << 4),
    /// <summary>Metal 4 (macOS 26+, iOS/iPadOS 26+, DXIL via Metal Shader Converter on macOS and iOS devices, pre-converted DXIL is accepted everywhere), available if "NRI_ENABLE_METAL_SUPPORT = ON" in CMake (https://developer.apple.com/metal/)</summary>
    METAL = (1 << 5),
}
