// A mesh-shader triangle: no vertex buffer, no input assembly, no task shader. One workgroup of
// one thread emits the three vertices and one primitive the release-gate triangle (Triangle.hlsl)
// draws, with the same per-vertex colours, so the readback can be checked the same way.
// Compiled to SPIR-V (SPV_EXT_mesh_shader) and DXIL (ms_6_5) by tools/compile-test-shaders.ps1.

struct Varyings
{
    float4 position : SV_Position;
    float3 color    : COLOR0;
};

[outputtopology("triangle")]
[numthreads(1, 1, 1)]
void msMain(out vertices Varyings verts[3], out indices uint3 tris[1])
{
    SetMeshOutputCounts(3, 1);

    // Clockwise in clip space (y up): top, bottom-right, bottom-left.
    const float2 positions[3] = { float2(0.0, 0.5), float2(0.5, -0.5), float2(-0.5, -0.5) };
    const float3 colors[3]    = { float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1) };

    [unroll]
    for (uint i = 0; i < 3; i++)
    {
        verts[i].position = float4(positions[i], 0.0, 1.0);
        verts[i].color = colors[i];
    }
    tris[0] = uint3(0, 1, 2);
}

float4 psMain(Varyings i) : SV_Target0
{
    return float4(i.color, 1.0);
}
