// MultisampleTests: the pixel shader of MsaaTriangle.hlsl for an integer colour attachment
// (RGBA8_UINT): the same colour as integers, since a float output does not match the attachment's
// type. Drawn with MsaaTriangle.hlsl's vertex shader.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

uint4 psMain() : SV_Target0
{
    return uint4(60, 220, 90, 255);
}
