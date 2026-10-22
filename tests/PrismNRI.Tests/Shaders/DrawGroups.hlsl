// Which work groups an indirect dispatch ran (IndirectTests): every thread of group (x, y, z)
// adds 1 to slot (z * 4 + y) * 4 + x of a 64-slot storage buffer, so a group that ran once leaves
// 4 in its slot and a slot no group owns stays 0.
// Binding: descriptor set 0 (register space 0), range 0 = one STORAGE_STRUCTURED_BUFFER at u0.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

[[vk::binding(0, 0)]] RWStructuredBuffer<uint> Groups : register(u0, space0);

[numthreads(4, 1, 1)]
void main(uint3 group : SV_GroupID)
{
    uint previous;
    InterlockedAdd(Groups[(group.z * 4 + group.y) * 4 + group.x], 1, previous);
}
