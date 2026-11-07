namespace SharpNRI.Native;

/// <summary>https://docs.vulkan.org/samples/latest/samples/extensions/fragment_shading_rate_dynamic/README.html https://microsoft.github.io/DirectX-Specs/d3d/VariableRateShading.html</summary>
[NativeTypeName("uint8_t")]
public enum ShadingRate : byte
{
    FRAGMENT_SIZE_1X1,
    FRAGMENT_SIZE_1X2,
    FRAGMENT_SIZE_2X1,
    FRAGMENT_SIZE_2X2,
    /// <summary>Require "features.additionalShadingRates"</summary>
    FRAGMENT_SIZE_2X4,
    FRAGMENT_SIZE_4X2,
    FRAGMENT_SIZE_4X4,
    MAX_NUM,
}
