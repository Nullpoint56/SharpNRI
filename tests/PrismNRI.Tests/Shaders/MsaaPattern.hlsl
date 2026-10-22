// MultisampleTests: a full-screen triangle whose pixel shader writes a colour that encodes the
// pixel's position, R = 4 * (x mod 64), G = 4 * (y mod 64), B = 128, A = 255: unique per pixel in a
// 64x64 target, and different for any two pixels of an aligned 2x2 block in a larger one. Every
// sample of a pixel gets the same value, so a resolved pixel equals it exactly and a resolve region
// shows which source pixels it took. Under coarse shading (variable rate shading) one invocation
// covers a block of pixels, so the block is one colour where per-pixel shading gives four.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

float4 vsMain(uint vertexId : SV_VertexID) : SV_Position
{
    // (-1, -1), (3, -1), (-1, 3): covers the whole viewport.
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(uv * 2.0 - 1.0, 0.0, 1.0);
}

float4 psMain(float4 position : SV_Position) : SV_Target0
{
    uint2 p = uint2(position.xy) & 63;
    return float4(p.x * 4, p.y * 4, 128, 255) / 255.0;
}
