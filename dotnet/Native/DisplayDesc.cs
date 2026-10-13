namespace PrismNRI.Native;

public partial struct DisplayDesc
{
    [NativeTypeName("nri::ChromaticityCoords")]
    public ChromaticityCoords redPrimary;

    [NativeTypeName("nri::ChromaticityCoords")]
    public ChromaticityCoords greenPrimary;

    [NativeTypeName("nri::ChromaticityCoords")]
    public ChromaticityCoords bluePrimary;

    [NativeTypeName("nri::ChromaticityCoords")]
    public ChromaticityCoords whitePoint;

    public float minLuminance;

    public float maxLuminance;

    public float maxFullFrameLuminance;

    public float sdrLuminance;

    [NativeTypeName("bool")]
    public byte isHDR;
}
