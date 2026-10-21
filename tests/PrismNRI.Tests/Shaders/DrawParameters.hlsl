// Draw parameters through NRI's documented path (NRI.hlsl, "Draw parameters") for IndirectTests:
// on Vulkan the BaseVertex and BaseInstance built-ins; on D3D12 below shader model 6.8, NRI's
// emulation, two root constants at b0, space 999 that NRI sets from each draw when the pipeline
// layout has ENABLE_DRAW_PARAMETERS_EMULATION (and, for indirect draws, from the first two words of
// each DrawBaseDesc / DrawIndexedBaseDesc record). The declarations are NRI.hlsl's, spelled out
// here because the test shaders compile without include paths.
// The colour is flat: red = baseVertex / 255, green = baseInstance / 255, blue and alpha 1.
// Vertex input: a float2 clip-space position, Vulkan location 0, D3D semantic POSITION0.
// Compiled to SPIR-V and DXIL by tools/compile-test-shaders.ps1.

struct Input
{
    [[vk::location(0)]] float2 position : POSITION;
};

struct Varyings
{
    float4 position              : SV_Position;
    nointerpolation float4 color : COLOR0;
};

#ifndef __spirv__
struct BaseAttributeConstants
{
    int baseVertex;
    uint baseInstance;
};

ConstantBuffer<BaseAttributeConstants> BaseAttributes : register(b0, space999);
#endif

Varyings vsMain(Input input
#ifdef __spirv__
    , [[vk::builtin("BaseVertex")]] int baseVertex : _SV_Nothing1
    , [[vk::builtin("BaseInstance")]] uint baseInstance : _SV_Nothing2
#endif
    )
{
#ifndef __spirv__
    int baseVertex = BaseAttributes.baseVertex;
    uint baseInstance = BaseAttributes.baseInstance;
#endif

    Varyings o;
    o.position = float4(input.position, 0.0, 1.0);
    o.color = float4(baseVertex / 255.0, baseInstance / 255.0, 1.0, 1.0);
    return o;
}

float4 psMain(Varyings input) : SV_Target0
{
    return input.color;
}
