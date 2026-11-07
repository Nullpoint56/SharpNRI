namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum Multiview : byte
{
    /// <summary>Destination "viewport" and/or "layer" must be set in shaders explicitly, "viewMask" for rendering can be &lt; than the one used for pipeline creation (D3D12 style) requires "features.flexibleMultiview" (METAL: native shaders only, DXIL lacks "SV_ViewID")</summary>
    FLEXIBLE,
    /// <summary>View instances go to statically assigned corresponding attachment layers, "viewMask" for rendering must match the one used for pipeline creation (VK style) requires "features.layerBasedMultiview"</summary>
    LAYER_BASED,
    /// <summary>View instances go to statically assigned corresponding viewports, "viewMask" for pipeline creation is unused (D3D11 style) requires "features.viewportBasedMultiview"</summary>
    VIEWPORT_BASED,
    MAX_NUM,
}
