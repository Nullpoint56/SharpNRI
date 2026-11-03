// Ray-tracing extension test, procedural geometry: one inline ray query per thread against a TLAS
// whose BLAS holds axis-aligned boxes (BottomLevelGeometryType::AABBS). The rays are those of
// RayQuery.hlsl: 16x16 from z = 0 towards +z over the field [-1, 1]^2. Every procedural candidate
// is committed at t = 1 without an intersection test of its own, so the map shows exactly the boxes
// the BLAS was built from: the box's primitive index + 1 where a ray hit one, 0 where it missed.
// Compiled by tools/compile-test-shaders.ps1 (cs_6_5).

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
    while (query.Proceed())
    {
        if (query.CandidateType() == CANDIDATE_PROCEDURAL_PRIMITIVE)
            query.CommitProceduralPrimitiveHit(1.0);
    }

    gHits[id.y * 16 + id.x] = query.CommittedStatus() == COMMITTED_PROCEDURAL_PRIMITIVE_HIT ? query.CommittedPrimitiveIndex() + 1 : 0u;
}
