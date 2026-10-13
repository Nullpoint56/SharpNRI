// The compute smoke test: one dispatch of one 64-thread group writes a known pattern into a storage
// buffer, which the test copies to a readback buffer and checks word for word.
// Binding: descriptor set 0 (register space 0), range 0 = one STORAGE_STRUCTURED_BUFFER at u0.
// Vulkan binding 0 in set 0 matches a device created with all-zero VKBindingOffsets.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

[[vk::binding(0, 0)]] RWStructuredBuffer<uint> Output : register(u0, space0);

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    Output[id.x] = 0xC0FFEE00u + id.x;
}
