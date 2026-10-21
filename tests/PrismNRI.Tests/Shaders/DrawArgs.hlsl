// Indirect arguments written by the GPU (IndirectTests): one group of eight threads copies the
// eight 32-bit root constants into a storage buffer, word i from constant i, which a draw then
// reads as two DrawDesc records.
// Binding: descriptor set 0 (register space 0), range 0 = one STORAGE_STRUCTURED_BUFFER at u0;
// root constants: register b0, space 1, 32 bytes (a push constant block on Vulkan).
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Constants
{
    uint4 first;
    uint4 second;
};

#ifdef __spirv__
[[vk::push_constant]] ConstantBuffer<Constants> Root;
#else
ConstantBuffer<Constants> Root : register(b0, space1);
#endif

[[vk::binding(0, 0)]] RWStructuredBuffer<uint> Arguments : register(u0, space0);

[numthreads(8, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    Arguments[id.x] = id.x < 4 ? Root.first[id.x] : Root.second[id.x - 4];
}
