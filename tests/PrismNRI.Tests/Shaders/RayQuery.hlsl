// Ray-tracing extension test: one inline ray query per thread against a TLAS holding one triangle,
// writing 1 for a hit and 0 for a miss. 16x16 rays from z = 0 towards +z; the triangle sits on
// z = 1 and covers the middle of the field. Compiled by tools/compile-test-shaders.ps1 (cs_6_5).

[[vk::binding(0, 0)]] RaytracingAccelerationStructure gTlas : register(t0, space0);
[[vk::binding(1, 0)]] RWStructuredBuffer<uint> gHits : register(u1, space0);

[numthreads(8, 8, 1)]
void csMain(uint3 id : SV_DispatchThreadID)
{
    float2 uv = ((float2)id.xy + 0.5) / 16.0 * 2.0 - 1.0;

    RayDesc ray;
    ray.Origin = float3(uv.x, uv.y, 0.0);
    ray.Direction = float3(0.0, 0.0, 1.0);
    ray.TMin = 0.0;
    ray.TMax = 10.0;

    RayQuery<RAY_FLAG_NONE> query;
    query.TraceRayInline(gTlas, RAY_FLAG_NONE, 0xFF, ray);
    query.Proceed();

    gHits[id.y * 16 + id.x] = query.CommittedStatus() == COMMITTED_TRIANGLE_HIT ? 1u : 0u;
}
