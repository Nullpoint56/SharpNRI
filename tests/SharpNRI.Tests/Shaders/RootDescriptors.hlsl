// Root descriptors only (RootDescriptorTests): one root constant buffer (b0), one root read-only
// structured buffer (t1) and one root read-write structured buffer (u2), all in the root register
// space 0 and no descriptor set. One 16-thread group copies the 16 constant words to Output[0..15]
// and the first 16 words of the structured buffer to Output[16..31].
// Vulkan: the root descriptors are push descriptors in set 0 (the root register space) and the
// binding is the register index (the test devices have all-zero VKBindingOffsets). D3D12: root CBV,
// SRV and UAV parameters.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Words
{
    uint4 values[4];
};

[[vk::binding(0, 0)]] ConstantBuffer<Words> Constants : register(b0, space0);
[[vk::binding(1, 0)]] StructuredBuffer<uint> Source : register(t1, space0);
[[vk::binding(2, 0)]] RWStructuredBuffer<uint> Output : register(u2, space0);

[numthreads(16, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    Output[id.x] = Constants.values[id.x / 4][id.x % 4];
    Output[16 + id.x] = Source[id.x];
}
