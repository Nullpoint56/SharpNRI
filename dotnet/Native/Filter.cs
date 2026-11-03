namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum Filter : byte
{
    NEAREST,
    LINEAR,
    MAX_NUM,
}
