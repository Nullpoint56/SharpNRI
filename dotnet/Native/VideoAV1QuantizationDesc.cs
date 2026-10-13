namespace PrismNRI.Native;

public partial struct VideoAV1QuantizationDesc
{
    [NativeTypeName("int8_t")]
    public sbyte deltaQYDc;

    [NativeTypeName("int8_t")]
    public sbyte deltaQUDc;

    [NativeTypeName("int8_t")]
    public sbyte deltaQUAc;

    [NativeTypeName("int8_t")]
    public sbyte deltaQVDc;

    [NativeTypeName("int8_t")]
    public sbyte deltaQVAc;

    [NativeTypeName("uint8_t")]
    public byte qmY;

    [NativeTypeName("uint8_t")]
    public byte qmU;

    [NativeTypeName("uint8_t")]
    public byte qmV;

    [NativeTypeName("uint8_t")]
    public byte usingQmatrix;

    [NativeTypeName("uint8_t")]
    public byte diffUvDelta;
}
