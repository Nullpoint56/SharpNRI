// A layout that mixes a descriptor set with root descriptors (RootDescriptorTests): the output is a
// read-write structured buffer in descriptor set 0 (u0, register space 0), bound once; the inputs
// are root descriptors in the root register space 1: a constant buffer (b0) whose first word is
// where to write, and a read-only structured buffer (t1) to copy. One 16-thread group writes
// Output[base + i] = Source[i].
// Vulkan: set 0 is the descriptor set, set 1 the push descriptor set of the root register space;
// the binding is the register index (all-zero VKBindingOffsets). D3D12: a descriptor table and a
// root CBV and SRV.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Placement
{
    uint base;
    uint3 unused;
};

[[vk::binding(0, 0)]] RWStructuredBuffer<uint> Output : register(u0, space0);
[[vk::binding(0, 1)]] ConstantBuffer<Placement> Where : register(b0, space1);
[[vk::binding(1, 1)]] StructuredBuffer<uint> Source : register(t1, space1);

[numthreads(16, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    Output[Where.base + id.x] = Source[id.x];
}
