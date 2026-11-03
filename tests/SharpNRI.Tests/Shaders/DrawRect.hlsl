// A rectangle per draw for RenderStateTests: no vertex buffer, six vertices from SV_VertexID, the
// rectangle (in pixels of the 64x64 target), its depth and its colour from root constants.
// The two triangles are clockwise on the target, i.e. front-facing, when x0 < x1; a rectangle
// given with x0 > x1 covers the same pixels mirrored, counter-clockwise, back-facing.
// Root constants: register b0, space 0 (D3D12 root constants; a push constant block on Vulkan),
// 48 bytes, visible to the vertex and pixel shaders.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Constants
{
    float4 rect;  // x0, y0, x1, y1 in pixels, y down
    float4 color; // the pixel shader's output
    float4 depth; // x: the depth of every vertex
};

#ifdef __spirv__
[[vk::push_constant]] ConstantBuffer<Constants> Root;
#else
ConstantBuffer<Constants> Root : register(b0, space0);
#endif

float4 vsMain(uint vertexId : SV_VertexID) : SV_Position
{
    // (x0,y0) (x1,y0) (x0,y1), then (x1,y0) (x1,y1) (x0,y1).
    const uint2 corners[6] = { uint2(0, 0), uint2(1, 0), uint2(0, 1), uint2(1, 0), uint2(1, 1), uint2(0, 1) };
    uint2 corner = corners[vertexId];
    float2 pixel = float2(corner.x != 0 ? Root.rect.z : Root.rect.x, corner.y != 0 ? Root.rect.w : Root.rect.y);
    return float4(pixel.x / 32.0 - 1.0, 1.0 - pixel.y / 32.0, Root.depth.x, 1.0);
}

float4 psMain() : SV_Target0
{
    return Root.color;
}
