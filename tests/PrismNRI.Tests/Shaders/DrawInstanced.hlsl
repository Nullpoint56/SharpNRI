// Instanced vertex input for DrawTests: a float2 clip-space position per vertex, and per instance
// a float2 clip-space offset and an RGBA8_UNORM colour. The colour is passed flat.
// Vertex input: Vulkan locations 0, 1, 2; D3D semantics POSITION0, OFFSET0, COLOR0.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Input
{
    [[vk::location(0)]] float2 position : POSITION; // per vertex
    [[vk::location(1)]] float2 offset   : OFFSET;   // per instance
    [[vk::location(2)]] float4 color    : COLOR;    // per instance
};

struct Varyings
{
    float4 position                : SV_Position;
    nointerpolation float4 color   : COLOR0;
};

Varyings vsMain(Input input)
{
    Varyings o;
    o.position = float4(input.position + input.offset, 0.0, 1.0);
    o.color = input.color;
    return o;
}

float4 psMain(Varyings input) : SV_Target0
{
    return input.color;
}
