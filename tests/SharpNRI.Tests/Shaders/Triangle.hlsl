// The release-gate triangle: no vertex buffer, no descriptors, no root constants. Three vertices
// from SV_VertexID, a fixed colour per vertex, rendered to a 64x64 RGBA8 attachment and read back.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Varyings
{
    float4 position : SV_Position;
    float3 color    : COLOR0;
};

Varyings vsMain(uint vertexId : SV_VertexID)
{
    // Clockwise in clip space (y up): top, bottom-right, bottom-left.
    float2 positions[3] = { float2(0.0, 0.5), float2(0.5, -0.5), float2(-0.5, -0.5) };
    float3 colors[3]    = { float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1) };

    Varyings o;
    o.position = float4(positions[vertexId], 0.0, 1.0);
    o.color = colors[vertexId];
    return o;
}

float4 psMain(Varyings i) : SV_Target0
{
    return float4(i.color, 1.0);
}
