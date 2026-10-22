// A root constant buffer on the graphics bind point (RootDescriptorTests): vsMain draws one
// triangle that covers the whole viewport from SV_VertexID, psMain returns the colour in the root
// constant buffer b0 (root register space 0, fragment stage only). No vertex buffer, no set.
// Vulkan: a push descriptor in set 0, binding 0 (all-zero VKBindingOffsets). D3D12: a root CBV.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Material
{
    float4 color;
};

[[vk::binding(0, 0)]] ConstantBuffer<Material> Surface : register(b0, space0);

float4 vsMain(uint vertexId : SV_VertexID) : SV_Position
{
    // (-1,-1), (3,-1), (-1,3): the viewport lies inside.
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(uv * 2.0 - 1.0, 0.0, 1.0);
}

float4 psMain() : SV_Target0
{
    return Surface.color;
}
