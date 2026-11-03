namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum QueueType : byte
{
    GRAPHICS,
    COMPUTE,
    COPY,
    VIDEO_DECODE,
    VIDEO_ENCODE,
    MAX_NUM,
}
