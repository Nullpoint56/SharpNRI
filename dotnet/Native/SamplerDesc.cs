namespace SharpNRI.Native;

public partial struct SamplerDesc
{
    [NativeTypeName("nri::Filters")]
    public Filters filters;

    [NativeTypeName("uint8_t")]
    public byte anisotropy;

    public float mipBias;

    public float mipMin;

    public float mipMax;

    [NativeTypeName("nri::AddressModes")]
    public AddressModes addressModes;

    [NativeTypeName("nri::CompareOp")]
    public CompareOp compareOp;

    [NativeTypeName("nri::Color")]
    public Color borderColor;

    [NativeTypeName("bool")]
    public byte isInteger;

    [NativeTypeName("bool")]
    public byte unnormalizedCoordinates;
}
