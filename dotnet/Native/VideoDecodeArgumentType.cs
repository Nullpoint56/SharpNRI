namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoDecodeArgumentType : byte
{
    PICTURE_PARAMETERS,
    INVERSE_QUANTIZATION_MATRIX,
    SLICE_CONTROL,
    MAX_NUM,
}
