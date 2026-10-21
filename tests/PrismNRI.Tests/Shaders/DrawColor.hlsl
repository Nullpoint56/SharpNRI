// Vertex-buffer input for DrawTests and IndirectTests: a float2 clip-space position and an
// RGBA8_UNORM colour per vertex. The colour is passed flat, so every pixel of a triangle has
// exactly the colour of its provoking vertex, the first of the triangle in both APIs.
// Vertex input: Vulkan locations 0 and 1; D3D semantics POSITION0 and COLOR0.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Input
{
    [[vk::location(0)]] float2 position : POSITION;
    [[vk::location(1)]] float4 color    : COLOR;
};

struct Varyings
{
    float4 position                : SV_Position;
    nointerpolation float4 color   : COLOR0;
};

Varyings vsMain(Input input)
{
    Varyings o;
    o.position = float4(input.position, 0.0, 1.0);
    o.color = input.color;
    return o;
}

float4 psMain(Varyings input) : SV_Target0
{
    return input.color;
}
