// MultisampleTests: one triangle in one constant colour, no vertex buffer, no descriptors. Every
// sample of a pixel holds either the clear colour or this colour, so a resolve can be checked
// exactly: AVERAGE gives k/N of one and (N-k)/N of the other, MIN and MAX the component-wise
// extremes, SAMPLE_ZERO one of the two.
// The edges slope in three directions, so they cross the pixel grid and the sample positions at
// varied offsets and some pixels are covered by only some of their samples.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

float4 vsMain(uint vertexId : SV_VertexID) : SV_Position
{
    // Clip space, y up.
    float2 positions[3] = { float2(-0.70, -0.62), float2(0.74, -0.30), float2(-0.18, 0.78) };
    return float4(positions[vertexId], 0.0, 1.0);
}

float4 psMain() : SV_Target0
{
    return float4(60.0, 220.0, 90.0, 255.0) / 255.0;
}
