namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum VertexStreamStepRate : byte
{
    PER_VERTEX,
    PER_INSTANCE,
    MAX_NUM,
}
