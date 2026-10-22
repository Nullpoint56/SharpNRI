// QueryTests: a quad of two triangles, six vertices from SV_VertexID, no vertex buffer, covering
// the whole viewport at depth 0.5 (the viewport's depth range then places it in front of or behind
// the depth buffer). The pixel shader writes a constant colour.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

float4 vsMain(uint vertexId : SV_VertexID) : SV_Position
{
    // Two triangles, (0, 1, 2) and (2, 1, 3) of the corners below, clip space.
    float2 corners[4] = { float2(-1.0, -1.0), float2(1.0, -1.0), float2(-1.0, 1.0), float2(1.0, 1.0) };
    uint indices[6] = { 0, 1, 2, 2, 1, 3 };
    return float4(corners[indices[vertexId]], 0.5, 1.0);
}

float4 psMain() : SV_Target0
{
    return float4(1.0, 0.5, 0.25, 1.0);
}
