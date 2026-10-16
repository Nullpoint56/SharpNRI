// GPU-based validation (D3D12): a descriptor table range of two storage buffers, u0 and u1, of
// which the test writes only slot 0, and a dispatch that writes through both. Only GPU-based
// validation sees which descriptors a shader actually touches at execution time, so only it
// reports the access through the never-written slot 1 (D3D12GpuValidationTests).
// Binding: descriptor set 0 (register space 0), range 0 = two STORAGE_STRUCTURED_BUFFERs at u0, u1.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1; only the DXIL is used.

[[vk::binding(0, 0)]] RWStructuredBuffer<uint> Outputs[2] : register(u0, space0);

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    Outputs[0][id.x] = 0xC0FFEE00u + id.x;
    Outputs[1][id.x] = 0xBAD00000u + id.x;
}
