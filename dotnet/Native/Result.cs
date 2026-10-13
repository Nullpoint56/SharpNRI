namespace PrismNRI.Native;

[NativeTypeName("int8_t")]
public enum Result : sbyte
{
    DEVICE_LOST = -3,
    OUT_OF_DATE = -2,
    INVALID_SDK = -1,
    SUCCESS = 0,
    FAILURE = 1,
    INVALID_ARGUMENT = 2,
    OUT_OF_MEMORY = 3,
    UNSUPPORTED = 4,
    MAX_NUM,
}
