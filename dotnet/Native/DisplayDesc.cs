namespace SharpNRI.Native;

/// <summary>Describes color settings and capabilities of the closest display: - Luminance provided in nits (cd/m2) - SDR = standard dynamic range - LDR = low dynamic range (in many cases LDR == SDR) - HDR = high dynamic range, assumes G2084: - BT709_G10_16BIT: HDR gets enabled and applied implicitly if Windows HDR is enabled - BT2020_G2084_10BIT: HDR requires explicit color conversions and enabled HDR in Windows - "SDR scale in HDR mode" = sdrLuminance / 80</summary>
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
