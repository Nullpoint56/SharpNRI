// Ray-tracing pipeline test: the RayQuery.hlsl scene traced through a pipeline instead of an inline
// query. 16x16 rays from z = 0 towards +z; the triangle on z = 1 covers the middle of the field.
// Each ray writes what answered it: 2 for the closest-hit shader, 1 for the miss shader, so 0 means
// the ray never ran. Compiled as one library (lib_6_3) by tools/compile-test-shaders.ps1; the
// pipeline names each entry point from the same bytecode.

[[vk::binding(0, 0)]] RaytracingAccelerationStructure gTlas : register(t0, space0);
[[vk::binding(1, 0)]] RWStructuredBuffer<uint> gHits : register(u1, space0);

struct Payload
{
    uint value;
};

[shader("raygeneration")]
void rayGen()
{
    uint2 id = DispatchRaysIndex().xy;
    float2 uv = ((float2)id + 0.5) / 16.0 * 2.0 - 1.0;

    RayDesc ray;
    ray.Origin = float3(uv.x, uv.y, 0.0);
    ray.Direction = float3(0.0, 0.0, 1.0);
    ray.TMin = 0.0;
    ray.TMax = 10.0;

    Payload payload;
    payload.value = 0;
    TraceRay(gTlas, RAY_FLAG_NONE, 0xFF, 0, 1, 0, ray, payload);

    gHits[id.y * 16 + id.x] = payload.value;
}

[shader("miss")]
void miss(inout Payload payload)
{
    payload.value = 1;
}

[shader("closesthit")]
void closestHit(inout Payload payload, in BuiltInTriangleIntersectionAttributes attributes)
{
    payload.value = 2;
}
