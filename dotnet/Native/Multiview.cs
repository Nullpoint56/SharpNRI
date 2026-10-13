namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum Multiview : byte
{
    FLEXIBLE,
    LAYER_BASED,
    VIEWPORT_BASED,
    MAX_NUM,
}
