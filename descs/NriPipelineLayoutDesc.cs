// Managed forms of the NRI descs that carry pointers to arrays.
//
// NRI's PipelineLayoutDesc points at arrays of RootConstantDesc, RootDescriptorDesc, RootSamplerDesc
// and DescriptorSetDesc, and each DescriptorSetDesc points at an array of DescriptorRangeDesc. A C#
// caller cannot hold a stable pointer to a managed array, so these are the same descs with arrays
// where NRI has pointer-plus-count, and nothing else changed: the field names are NRI's and the
// leaf structs are the generated ones. PrismNRI pins and marshals at the create call.
//
// They live in PrismNRI.Descs because a library that projects shader reflection into a pipeline
// layout has to construct them without a device or the native library in hand.

using PrismNRI.Native;

namespace PrismNRI;

/// <summary><c>DescriptorSetDesc</c> with its ranges as an array.</summary>
/// <param name="RegisterSpace">Must be unique across the layout; avoid big gaps.</param>
/// <param name="Ranges">The descriptor ranges in this set.</param>
public sealed record NriDescriptorSetDesc(
    uint RegisterSpace,
    DescriptorRangeDesc[] Ranges);

/// <summary><c>PipelineLayoutDesc</c> with its arrays as arrays.</summary>
public sealed class NriPipelineLayoutDesc
{
    /// <summary>Register space of the root constants, descriptors and samplers.</summary>
    public uint RootRegisterSpace { get; init; }

    public RootConstantDesc[] RootConstants { get; init; } = [];
    public RootDescriptorDesc[] RootDescriptors { get; init; } = [];
    public RootSamplerDesc[] RootSamplers { get; init; } = [];
    public NriDescriptorSetDesc[] DescriptorSets { get; init; } = [];

    /// <summary>
    /// Every stage any part of the layout is visible to. <c>StageBits.ALL</c> is 0 in NRI (the
    /// barrier default) and the backends read it here as every shader stage; patch 0016 makes NRI
    /// validation agree (upstream masked each range's stages with 0 and refused the layout as
    /// "not compatible with 'shaderStages'").
    /// </summary>
    public StageBits ShaderStages { get; init; }

    public PipelineLayoutBits Flags { get; init; }
}

/// <summary><c>ShaderDesc</c> with its bytecode as bytes.</summary>
/// <param name="Stage">Exactly one stage bit.</param>
/// <param name="Bytecode">SPIR-V on Vulkan, DXIL on D3D12; see <c>DeviceDesc.shaderFeatures</c> for what the device accepts.</param>
/// <param name="EntryPointName">Optional; NRI defaults to <c>main</c>.</param>
/// <param name="ThreadGroupSizeX">Metal only; ignored on Vulkan and D3D12.</param>
public sealed record NriShaderDesc(
    StageBits Stage,
    byte[] Bytecode,
    string? EntryPointName = null,
    ushort ThreadGroupSizeX = 0,
    ushort ThreadGroupSizeY = 0,
    ushort ThreadGroupSizeZ = 0);

/// <summary><c>VertexAttributeDesc</c> with the D3D semantic as a string.</summary>
/// <param name="SemanticName">D3D: the HLSL semantic. Ignored on Vulkan.</param>
/// <param name="SemanticIndex">D3D: the semantic's index.</param>
/// <param name="Location">Vulkan: the <c>location</c> decoration. Ignored on D3D.</param>
/// <param name="Offset">Byte offset within the vertex.</param>
/// <param name="Format">The attribute's format.</param>
/// <param name="StreamIndex">Index into <see cref="NriVertexInputDesc.Streams"/>.</param>
public readonly record struct NriVertexAttributeDesc(
    string? SemanticName,
    uint SemanticIndex,
    uint Location,
    uint Offset,
    Format Format,
    ushort StreamIndex);

/// <summary><c>VertexInputDesc</c> with its arrays as arrays.</summary>
public sealed class NriVertexInputDesc
{
    public NriVertexAttributeDesc[] Attributes { get; init; } = [];
    public VertexStreamDesc[] Streams { get; init; } = [];
}
